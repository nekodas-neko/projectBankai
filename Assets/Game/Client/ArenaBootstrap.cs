using System.Collections.Generic;
using System.Net;
using Game.Core;
using Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    public enum SessionMode
    {
        Solo = 0,
        Host = 1,
        Join = 2,
    }

    public class ArenaBootstrap : MonoBehaviour
    {
        const string DungeonId = "slime_cave";
        const float MaxFrameSeconds = 0.1f;
        const float CameraSmoothing = 8f;

        static readonly Vector3 CameraOffset = new Vector3(0f, 26f, -12f);

        public ArenaAssets assets;

        readonly List<DamageEvent> events = new List<DamageEvent>();
        readonly List<UseEvent> uses = new List<UseEvent>();
        LocalSession session;
        UdpGameHost host;
        UdpClientTransport transport;
        LanScanner scanner;
        SessionMenu menu;
        Registry<ClassDef> classDefs;
        EntityViews views;
        AimIndicator aimIndicator;
        Telegraphs telegraphs;
        BossTargetMarker bossMarker;
        AggroRings aggroRings;
        MapView mapView;
        ClassDef playerClass;
        MasteryProgress mastery;
        readonly FightStats fightStats = new FightStats();
        Registry<SkillTreeDef> skillTrees;
        SkillTreePanel skillPanel;
        CharacterScreen characterScreen;
        string skillMessage = string.Empty;
        Registry<AbilityDef> abilityDefs;
        Registry<DungeonDef> dungeonDefs;
        int[] loadout = new int[0];
        Camera viewCamera;
        SessionMode mode;
        string hostAddress;
        string notice;
        bool swapRequested;
        bool menuOpenedForFailure;

        public LocalSession Session => session;

        public UdpGameHost Host => host;

        public UdpClientTransport Transport => transport;

        public GameClient LocalClient { get; private set; }

        public Hud Hud { get; private set; }

        public EntityViews Views => views;

        void Start()
        {
            DefinitionLoader.Load(out classDefs, out abilityDefs, out dungeonDefs);

            var className = HudSettings.ClassName;
            playerClass = classDefs.Get(className);
            skillTrees = SkillTreeCatalog.Load(classDefs, abilityDefs);
            var masteryRules = MasteryCatalog.Load();
            var weaponModifiers = ModifierCatalog.Load();
            var armorSets = ArmorCatalog.Load();
            var lootTables = LootCatalog.Load(classDefs, weaponModifiers, armorSets);
            lootTables.TryGet(LootTableAsset.BossDrop, out var bossDrop);
            mastery = new MasteryProgress(ProfileStore.ForDevice(), masteryRules, bossDrop, 0, new RewardBook(classDefs, masteryRules), lootTables);
            mastery.UseModifiers(weaponModifiers);
            mastery.UseArmorSets(armorSets);
            mastery.Equip(className);
            loadout = Loadout(className);
            views = new EntityViews(transform, assets);
            aimIndicator = new AimIndicator(transform, assets.cube, assets.aim);
            telegraphs = new Telegraphs(transform, assets.cube, assets.cylinder, assets.telegraph, assets.telegraphFill);
            bossMarker = new BossTargetMarker(transform, assets.cylinder, assets.telegraphFill);
            aggroRings = new AggroRings(transform, assets.telegraphFill);
            viewCamera = Camera.main;
            BuildHud();

            mode = (SessionMode)HudSettings.SessionMode;
            if (mode == SessionMode.Join && !StartJoin(className))
            {
                mode = SessionMode.Solo;
            }

            if (mode != SessionMode.Join)
            {
                StartWorld(className);
                if (mode == SessionMode.Host && !StartHost())
                {
                    mode = SessionMode.Solo;
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                mastery?.Flush();
            }
        }

        void OnDestroy()
        {
            mastery?.Flush();
            BuildNumberOverlay.Extra = string.Empty;
            host?.Dispose();
            transport?.Dispose();
            scanner?.Dispose();
        }

        void Update()
        {
            if (swapRequested)
            {
                swapRequested = false;
                HudSettings.MoveOnRight = !HudSettings.MoveOnRight;
                var open = menu != null && menu.IsOpen;
                Destroy(Hud.Root);
                BuildHud();
                menu.SetOpen(open);
            }

            var dt = Mathf.Min(Time.deltaTime, MaxFrameSeconds);
            mastery.Observe(LocalClient?.Latest);
            fightStats.Step(LocalClient?.Latest, dt);
            mastery.Tick(dt);
            BuildNumberOverlay.Extra = mastery.Summary();
            var input = Hud.MoveStick.Value;
            if (input == Vector2.zero)
            {
                input = KeyboardInput();
            }

            if (session != null)
            {
                host?.Poll(dt);
                LocalClient.SetMoveInput(new Vec2(input.x, input.y));
                session.Advance(dt);
            }
            else if (transport != null)
            {
                transport.Update(dt);
                if (LocalClient == null && transport.State == ConnectionState.Connected)
                {
                    LocalClient = new GameClient(transport.PlayerId, transport, playerClass, new ClientConfig { Simulation = transport.Simulation.WithMap(WorldMap.From(dungeonDefs.Get(DungeonId))) }, mastery.Gear.Speed);
                }

                if (LocalClient != null && transport.State == ConnectionState.Connected)
                {
                    LocalClient.SetMoveInput(new Vec2(input.x, input.y));
                    LocalClient.Update(dt);
                }
            }

            UpdateMenu(dt);
            telegraphs.Tick(dt);

            if (LocalClient == null || !LocalClient.HasLocalEntity)
            {
                return;
            }

            views.Sync(LocalClient, dt);
            UpdateMap();
            SyncTelegraphs();
            UpdateBossTarget();
            aggroRings.Sync(LocalClient.Latest, LocalClient.LocalEntity, views);
            UpdateButtons();

            rejected.Clear();
            LocalClient.DrainRejected(rejected);
            uses.Clear();
            LocalClient.DrainUses(uses);
            for (var i = 0; i < uses.Count; i++)
            {
                PlayUse(uses[i]);
            }

            events.Clear();
            LocalClient.DrainEvents(events);
            for (var i = 0; i < events.Count; i++)
            {
                PlayHit(events[i]);
                fightStats.Add(events[i], LocalClient.Latest);
            }
        }

        void LateUpdate()
        {
            if (viewCamera == null || views == null || views.LocalRoot == null)
            {
                return;
            }

            var target = views.LocalRoot.position + CameraOffset * HudSettings.CameraZoom;
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, target, 1f - Mathf.Exp(-CameraSmoothing * Time.deltaTime));
        }

        void StartWorld(string className)
        {
            session = new LocalSession(new ServerConfig(), classDefs, abilityDefs, seed: 1);
            LocalClient = session.AddPlayer(new PlayerId(1), className, StableId.From(HudSettings.SkinFor(className)), loadout, mastery.WeaponPower, mastery.Gear.Health, mastery.Gear.Speed);
            session.StartDungeon(dungeonDefs.Get(DungeonId));
        }

        bool StartHost()
        {
            try
            {
                host = new UdpGameHost(session.Server, classDefs, Protocol.DefaultPort, DeviceName());
                hostAddress = string.Join("\n", LocalNetwork.Addresses(host.Port));
                return true;
            }
            catch (System.Net.Sockets.SocketException exception)
            {
                notice = $"Could not host: {exception.SocketErrorCode}. Running solo.";
                HudSettings.SessionMode = (int)SessionMode.Solo;
                return false;
            }
        }

        bool StartJoin(string className)
        {
            if (!TryParseEndpoint(HudSettings.JoinAddress, out var endpoint))
            {
                notice = string.IsNullOrWhiteSpace(HudSettings.JoinAddress)
                    ? "Type the host's address first. Running solo."
                    : $"'{HudSettings.JoinAddress}' is not a valid address. Running solo.";
                HudSettings.SessionMode = (int)SessionMode.Solo;
                return false;
            }

            try
            {
                transport = new UdpClientTransport(endpoint, className, StableId.From(HudSettings.SkinFor(className)), DeviceName(), loadout, mastery.WeaponPower, mastery.Gear.Health, mastery.Gear.Speed);
                return true;
            }
            catch (System.Net.Sockets.SocketException exception)
            {
                notice = $"Could not start joining: {exception.SocketErrorCode}. Running solo.";
                HudSettings.SessionMode = (int)SessionMode.Solo;
                return false;
            }
        }

        static bool TryParseEndpoint(string text, out IPEndPoint endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Trim().Split(':');
            var port = Protocol.DefaultPort;
            if (!IPAddress.TryParse(parts[0], out var address) || (parts.Length > 1 && !int.TryParse(parts[1], out port)) || port < 1 || port > 65535)
            {
                return false;
            }

            endpoint = new IPEndPoint(address, port);
            return true;
        }

        static string DeviceName()
        {
            var name = SystemInfo.deviceModel;
            return string.IsNullOrWhiteSpace(name) ? "Player" : name.Length > Protocol.MaxNameLength ? name.Substring(0, Protocol.MaxNameLength) : name;
        }

        void BuildHud()
        {
            Hud = Hud.Create(HudSettings.MoveOnRight, new HudCallbacks
            {
                SwapSides = RequestSwapSides,
                Zoom = ChangeZoom,
                CycleSkin = CycleSkin,
                ToggleCharacter = ToggleCharacter,
                ToggleSkills = ToggleSkills,
                ToggleSession = ToggleMenu,
                Restart = () => LocalClient?.Restart(),
            });

            menu = new SessionMenu(
                (RectTransform)Hud.Root.transform,
                () => SwitchMode(SessionMode.Solo, null),
                () => SwitchMode(SessionMode.Host, null),
                address => SwitchMode(SessionMode.Join, address),
                ToggleMenu);
            characterScreen = new CharacterScreen((RectTransform)Hud.Root.transform, Equip, TakeApart, RefreshCharacter, ToggleCharacter);
            skillPanel = new SkillTreePanel((RectTransform)Hud.Root.transform, ChooseSkillNode, ResetSkills, ToggleSkills);
            WireButtons();
        }

        void ToggleSkills()
        {
            var open = !skillPanel.IsOpen;
            skillMessage = string.Empty;
            skillPanel.SetOpen(open);
            if (open)
            {
                menu.SetOpen(false);
                characterScreen.SetOpen(false);
                RefreshSkills();
            }
        }

        void RefreshSkills()
        {
            var weapon = HudSettings.ClassName;
            skillTrees.TryGet(weapon, out var tree);
            skillPanel.Show(weapon, tree, mastery.Profile.MasteryFor(weapon), mastery.Rules, skillMessage);
        }

        // The loadout is sent when joining, so a change restarts the session (weapons will be changed in the hub, later).
        void ChooseSkillNode(int index)
        {
            var weapon = HudSettings.ClassName;
            if (!skillTrees.TryGet(weapon, out var tree) || index >= tree.Nodes.Length)
            {
                return;
            }

            if (mastery.TryChoose(tree, weapon, tree.Nodes[index].Id, out var reason))
            {
                Reload();
                return;
            }

            skillMessage = char.ToUpperInvariant(reason[0]) + reason.Substring(1);
            RefreshSkills();
        }

        void ResetSkills()
        {
            mastery.ResetSkills(HudSettings.ClassName);
            Reload();
        }

        void ToggleMenu()
        {
            var open = !menu.IsOpen;
            if (open)
            {
                skillPanel.SetOpen(false);
                characterScreen.SetOpen(false);
            }

            menu.SetOpen(open);
            scanner?.Dispose();
            scanner = null;
            if (open)
            {
                try
                {
                    scanner = new LanScanner();
                }
                catch (System.Net.Sockets.SocketException)
                {
                    notice = "Could not scan this network.";
                }
            }
        }

        void SwitchMode(SessionMode next, string address)
        {
            HudSettings.SessionMode = (int)next;
            if (address != null)
            {
                HudSettings.JoinAddress = address;
            }

            Reload();
        }

        void UpdateMenu(float dt)
        {
            if (menu == null)
            {
                return;
            }

            if (transport != null && transport.State != ConnectionState.Connecting && transport.State != ConnectionState.Connected && !menuOpenedForFailure)
            {
                menuOpenedForFailure = true;
                menu.SetOpen(true);
            }

            if (!menu.IsOpen)
            {
                return;
            }

            menu.SetStatus(StatusText());
            if (scanner != null)
            {
                scanner.Update(dt);
                menu.SetFound(scanner.Results);
            }
        }

        string StatusText()
        {
            string text;
            switch (mode)
            {
                case SessionMode.Host:
                    text = $"Hosting. Others type one of:\n{hostAddress}\nPlayers: {host.RemotePlayerCount + 1}";
                    break;
                case SessionMode.Join:
                    text = JoinStatus();
                    break;
                default:
                    text = "Solo, offline";
                    break;
            }

            return string.IsNullOrEmpty(notice) ? text : notice + "\n" + text;
        }

        string JoinStatus()
        {
            switch (transport.State)
            {
                case ConnectionState.Connecting:
                    return $"Connecting to {HudSettings.JoinAddress}...";
                case ConnectionState.Connected:
                    return $"Joined {HudSettings.JoinAddress}";
                case ConnectionState.Rejected:
                    return transport.RejectReason == Protocol.RejectFull ? "That session is full." : "The host does not know this class.";
                case ConnectionState.Failed:
                    return $"No answer from {HudSettings.JoinAddress}. Is the host on the same Wi-Fi?";
                default:
                    return "Disconnected from the host.";
            }
        }

        void WireButtons()
        {
            Hud.Attack.Fired += aim => Act(0, aim, false);
            Hud.Potion.Fired += _ => DrinkPotion();
            Hud.Attack.FiredHeavy += aim => Act(0, aim, true);
            Hud.Special.Fired += aim => Act(1, aim, false);
            Hud.Ultimate.Fired += aim => Act(Entity.UltimateSlot, aim, false);
            Hud.Defense.Fired += aim => Defend(aim);
        }

        float potionReadyAt;
        readonly List<int> rejected = new List<int>();

        // The ability the player has in a slot, by the same layout the server builds an entity with.
        string AbilityIdFor(int slot)
        {
            if (slot == 0)
            {
                return playerClass.AutoAttack;
            }

            if (slot == Entity.DefenseSlot)
            {
                return playerClass.Defense;
            }

            return slot - 1 < playerClass.Skills.Length ? playerClass.Skills[slot - 1] : null;
        }

        // A button press: the command goes to the server and the effect starts here at once. The server still decides; its echo of
        // this action is dropped by the client, so nothing plays twice.
        void Act(int slot, Vector2 screenAim, bool heavy)
        {
            if (LocalClient == null)
            {
                return;
            }

            var aim = ToWorld(screenAim);
            var id = slot == 0 ? (heavy ? LocalClient.HeavyAttack(aim) : LocalClient.Attack(aim)) : LocalClient.Skill(slot, aim);
            PredictUse(id, slot, aim);
        }

        void Defend(Vector2 screenAim)
        {
            if (LocalClient == null)
            {
                return;
            }

            var direction = DefendDirection(screenAim);
            var dashDistance = 0f;
            var dashSeconds = 0f;
            if (abilityDefs.TryGet(playerClass.Defense, out var defense) && defense.Kind == AbilityKind.Dodge && CanPredict(Entity.DefenseSlot, defense))
            {
                dashDistance = defense.DashDistance;
                dashSeconds = Mathf.Max(defense.Duration, 1f / 30f);
            }

            var id = LocalClient.Defend(direction, dashDistance, dashSeconds);
            PredictUse(id, Entity.DefenseSlot, direction);
        }

        // Only an action that looks like it will be accepted is shown early: off cooldown, affordable, and the ultimate charged.
        bool CanPredict(int slot, AbilityDef ability)
        {
            if (!LocalClient.TryGetLocalState(out var state) || state.Health <= 0f || state.GetCooldown(slot) > 0f)
            {
                return false;
            }

            if (slot == Entity.UltimateSlot && (state.ChargeMax <= 0f || state.Charge < state.ChargeMax))
            {
                return false;
            }

            var variant = VariantOf(ability, slot);
            var cost = ability.ResourceCost * (variant != null ? variant.CostScale : 1f);
            return cost <= 0f || state.MaxResource <= 0f || state.Resource >= cost;
        }

        void PredictUse(int id, int slot, Vec2 aim)
        {
            var abilityId = AbilityIdFor(slot);
            if (id == 0 || !abilityDefs.TryGet(abilityId, out var ability) || !CanPredict(slot, ability))
            {
                return;
            }

            var variant = slot < loadout.Length ? loadout[slot] : 0;
            var use = new UseEvent
            {
                User = LocalClient.LocalEntity,
                Slot = (byte)slot,
                Ability = StableId.From(abilityId),
                Variant = variant,
                Direction = aim.SqrLength > 1e-6f ? aim : AimAtNearest(ability.Range),
                Sequence = id,
            };
            PlayUse(use);
            LocalClient.ExpectEcho(id);
        }

        // A tap with no aim goes at the boss or the nearest enemy in range the way the server picks it, otherwise the way the player faces.
        Vec2 AimAtNearest(float range)
        {
            var latest = LocalClient.Latest;
            var from = LocalClient.PredictedPosition;
            var best = float.MaxValue;
            var found = false;
            var target = default(Vec2);
            var bossFound = false;
            for (var i = 0; latest != null && i < latest.Entities.Count; i++)
            {
                var e = latest.Entities[i];
                if (e.Team != Team.Enemies || e.Health <= 0f)
                {
                    continue;
                }

                var boss = (e.Effects & EffectFlags.Boss) != 0;
                var distance = (e.Position - from).Length;
                if (distance > range || (bossFound && !boss))
                {
                    continue;
                }

                if ((boss && !bossFound) || distance < best)
                {
                    best = distance;
                    target = e.Position - from;
                    found = true;
                    bossFound |= boss;
                }
            }

            return found && target.SqrLength > 1e-6f ? target.Normalized : LocalClient.Facing;
        }

        // Drinks a potion if there is one in the bag, the potion is ready and it would heal something. The server decides again.
        void DrinkPotion()
        {
            if (LocalClient == null || mastery == null || Time.time < potionReadyAt || !LocalClient.TryGetLocalState(out var state))
            {
                return;
            }

            if (state.Health <= 0f || state.Health >= state.MaxHealth || !mastery.TryUsePotion())
            {
                return;
            }

            LocalClient.UsePotion();
            potionReadyAt = Time.time + new ServerConfig().PotionCooldownSeconds;
        }

        void UpdatePotionButton()
        {
            var cooldown = new ServerConfig().PotionCooldownSeconds;
            var left = Mathf.Clamp01((potionReadyAt - Time.time) / cooldown);
            Hud.Potion.SetLabel($"Potion\nx{mastery.Potions}");
            Hud.Potion.SetCooldown(mastery.Potions <= 0 ? 1f : left);
        }

        void UpdateButtons()
        {
            var hasState = LocalClient.TryGetLocalState(out var state);
            UpdateButton(Hud.Attack, hasState, state, 0, playerClass.AutoAttack);
            UpdateButton(Hud.Special, hasState, state, 1, playerClass.Skills.Length > 0 ? playerClass.Skills[0] : null);
            UpdateUltimate(hasState, state);
            UpdateButton(Hud.Defense, hasState, state, Entity.DefenseSlot, playerClass.Defense);
            Hud.Defense.SetLabel(DefenseLabel());
            UpdateResource(hasState, state);
            UpdatePotionButton();
            var quests = QuestLog.Build(dungeonDefs.Get(DungeonId), LocalClient.Latest);
            Hud.Quest.Show(quests);
            Hud.Banner.Update(LocalClient.Latest, hasState && state.Health <= 0f, LocalClient.Map != null, QuestLog.Current(quests));
            var phase = LocalClient.Latest?.Dungeon ?? DungeonPhase.None;
            Hud.Banner.SetSummary(phase == DungeonPhase.Victory || phase == DungeonPhase.Defeat ? fightStats.Report(LocalClient.Player) : string.Empty);

            var aiming = Hud.Attack.IsAiming ? Hud.Attack : Hud.Special.IsAiming ? Hud.Special : null;
            if (aiming == null || views.LocalRoot == null)
            {
                aimIndicator.Hide();
                return;
            }

            var id = aiming == Hud.Attack ? playerClass.AutoAttack : playerClass.Skills[0];
            if (abilityDefs.TryGet(id, out var ability))
            {
                var world = ToWorld(aiming.Aim);
                var variant = VariantOf(ability, aiming == Hud.Attack ? 0 : 1);
                var rangeScale = variant != null ? variant.RangeScale : 1f;
                aimIndicator.Show(views.LocalRoot.position, new Vector2(world.X, world.Y), ability.Range * rangeScale, ability.Radius);
            }
        }

        void UpdateUltimate(bool hasState, EntityState state)
        {
            var id = playerClass.Skills.Length > Entity.UltimateSlot - 1 ? playerClass.Skills[Entity.UltimateSlot - 1] : null;
            if (!hasState || state.ChargeMax <= 0f || !abilityDefs.TryGet(id, out var ability))
            {
                Hud.Ultimate.SetCooldown(1f);
                Hud.Ultimate.SetLabel("Ult");
                return;
            }

            var charge = Mathf.Clamp01(state.Charge / state.ChargeMax);
            var cooldown = ability.Cooldown > 0f ? state.GetCooldown(Entity.UltimateSlot) / ability.Cooldown : 0f;
            Hud.Ultimate.SetCooldown(Mathf.Max(1f - charge, cooldown));
            Hud.Ultimate.SetLabel(charge >= 1f ? "ULT!" : Mathf.FloorToInt(charge * 100f) + "%");
        }

        void UpdateButton(ActionButton button, bool hasState, EntityState state, int slot, string abilityId)
        {
            if (!hasState || !abilityDefs.TryGet(abilityId, out var ability))
            {
                button.SetCooldown(0f);
                return;
            }

            var cooldown = ability.Cooldown > 0f ? state.GetCooldown(slot) / (ability.Cooldown * CooldownScale(ability, slot)) : 0f;
            var variant = VariantOf(ability, slot);
            var cost = ability.ResourceCost * (variant != null ? variant.CostScale : 1f);
            button.SetCooldown(ActionButton.RingFill(cooldown, state.Resource, state.MaxResource, cost));
        }

        // The variants the equipped weapon's skill tree choices select, one per ability slot.
        int[] Loadout(string className)
        {
            if (!skillTrees.TryGet(className, out var tree))
            {
                return new int[Entity.AbilitySlots];
            }

            var weapon = mastery.Profile.MasteryFor(className);
            SkillTreeRules.Sanitize(tree, weapon);
            return SkillTreeRules.Loadout(tree, weapon);
        }

        float CooldownScale(AbilityDef ability, int slot)
        {
            var variant = VariantOf(ability, slot);
            return variant != null ? variant.CooldownScale : 1f;
        }

        VariantDef VariantOf(AbilityDef ability, int slot)
        {
            if (slot >= loadout.Length || loadout[slot] == 0)
            {
                return null;
            }

            for (var i = 0; i < ability.Variants.Length; i++)
            {
                if (StableId.From(ability.Variants[i].Id) == loadout[slot])
                {
                    return ability.Variants[i];
                }
            }

            return null;
        }

        string DefenseLabel()
        {
            if (!abilityDefs.TryGet(playerClass.Defense, out var defense))
            {
                return "-";
            }

            switch (defense.Kind)
            {
                case AbilityKind.Dodge: return "Dodge";
                case AbilityKind.Block: return "Block";
                case AbilityKind.Shield: return "Ward";
                default: return "Defend";
            }
        }

        void UpdateResource(bool hasState, EntityState state)
        {
            if (!hasState || playerClass.Resource == null)
            {
                Hud.Resource.Hide();
                return;
            }

            Hud.Resource.Show(playerClass.Resource.Id, state.Resource, state.MaxResource);
        }

        Vec2 DefendDirection(Vector2 dragAim)
        {
            if (dragAim.sqrMagnitude > 1e-6f)
            {
                return ToWorld(dragAim);
            }

            var stick = Hud.MoveStick.Value;
            if (stick == Vector2.zero)
            {
                stick = KeyboardInput();
            }

            return ToWorld(stick);
        }

        Vec2 ToWorld(Vector2 screenAim)
        {
            if (screenAim.sqrMagnitude < 1e-6f)
            {
                return Vec2.Zero;
            }

            var pitch = viewCamera != null ? viewCamera.transform.eulerAngles.x : 60f;
            var scale = Mathf.Max(0.3f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            return new Vec2(screenAim.x, screenAim.y / scale).Normalized;
        }

        // A dungeon with zones is drawn from its map: floors, walls and a barrier in every closed gate, plus the mini map.
        void UpdateMap()
        {
            var map = LocalClient.Map;
            if (map == null)
            {
                return;
            }

            if (mapView == null)
            {
                BuildMap(map);
            }

            mapView.Refresh();
            var here = LocalClient.PredictedPosition;
            mapView.Fade(here.X, here.Y);
            views.FogRadius = MapView.ShowDistance + 14f;
            Hud.MiniMap.Refresh(LocalClient.Latest, LocalClient.LocalEntity);
        }

        void BuildMap(WorldMap map)
        {
            var ground = GameObject.Find("Ground");
            var groundMaterial = ground != null && ground.GetComponent<Renderer>() != null ? ground.GetComponent<Renderer>().sharedMaterial : assets.dead;
            var props = GameObject.Find("TemporaryProps");
            mapView = new MapView(transform, map, assets.cube, groundMaterial, assets.telegraphFill);
            MapScenery.Decorate(mapView, map, props != null ? props.transform : null, assets.cube, groundMaterial, assets.telegraphFill);

            // A dungeon is dark: no sky behind the walls.
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            }

            foreach (var name in new[] { "Ground", "Rim", "TemporaryProps" })
            {
                var round = GameObject.Find(name);
                if (round != null)
                {
                    round.SetActive(false);
                }
            }

            Hud.MiniMap.Setup(map);
        }

        void UpdateBossTarget()
        {
            var snapshot = LocalClient.Latest;
            var entities = snapshot?.Entities;
            var found = false;
            var boss = default(EntityState);
            for (var i = 0; entities != null && i < entities.Count; i++)
            {
                if ((entities[i].Effects & EffectFlags.Boss) != 0 && entities[i].Health > 0f)
                {
                    boss = entities[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Hud.BossFrame.Hide();
                bossMarker.Hide();
                return;
            }

            var targetState = default(EntityState);
            var hasTarget = boss.Target.IsValid && snapshot.TryFind(boss.Target, out targetState);
            var isLocal = hasTarget && targetState.Id == LocalClient.LocalEntity;
            var staggered = (boss.Effects & EffectFlags.Staggered) != 0;
            Hud.BossFrame.Show(boss.Health, boss.MaxHealth, hasTarget, isLocal, hasTarget ? targetState.Owner.Value : 0, staggered);
            UpdateBossCast(snapshot, boss);
            if (hasTarget && views.TryGetPosition(boss.Target, out var position))
            {
                bossMarker.Show(position, Time.time);
            }
            else
            {
                bossMarker.Hide();
            }
        }

        // While the boss winds up a cast that can be interrupted, a bar under its frame and one above its head count it down.
        void UpdateBossCast(Snapshot snapshot, EntityState boss)
        {
            var windups = snapshot?.Windups;
            for (var i = 0; windups != null && i < windups.Count; i++)
            {
                if (windups[i].User == boss.Id && AbilityCatalog.TryGet(windups[i].Ability, out var cast) && cast.interruptible && cast.windup > 0f)
                {
                    var fraction = 1f - Mathf.Clamp01(windups[i].Remaining / cast.windup);
                    Hud.BossFrame.ShowCast(cast.name, fraction);
                    views.SetCast(boss.Id, fraction);
                    return;
                }
            }

            Hud.BossFrame.HideCast();
            views.SetCast(boss.Id, -1f);
        }

        void SyncTelegraphs()
        {
            telegraphs.BeginSync();
            var windups = LocalClient.Latest?.Windups;
            for (var i = 0; windups != null && i < windups.Count; i++)
            {
                var windup = windups[i];
                if (!AbilityCatalog.TryGet(windup.Ability, out var ability) || ability.windup <= 0f)
                {
                    continue;
                }

                var range = ability.range;
                var radius = ability.radius;
                if (ability.TryGetVariant(windup.Variant, out var variant))
                {
                    range *= variant.rangeScale;
                    radius = ability.shape == AbilityShape.Circle ? radius * variant.rangeScale : radius;
                }

                var key = unchecked((windup.User.Value * 397) ^ windup.Ability) | 1;
                telegraphs.Show(key, new Vector3(windup.Origin.X, 0f, windup.Origin.Y), new Vector2(windup.Direction.X, windup.Direction.Y), ability.shape, range, radius, ability.angle, ability.windup, windup.Remaining, ability.avoidable);
            }

            telegraphs.EndSync();
        }

        void PlayUse(UseEvent use)
        {
            views.OnUse(use.User, use.Slot);
            if (!AbilityCatalog.TryGet(use.Ability, out var ability) || ability.castEffect == null || !views.TryGetPosition(use.User, out var position))
            {
                return;
            }

            var direction = new Vector3(use.Direction.X, 0f, use.Direction.Y);
            var cast = ability.castEffect;
            if (ability.TryGetVariant(use.Variant, out var variant) && variant.castEffect != null)
            {
                cast = variant.castEffect;
            }

            var rotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            EffectPlayer.Play(cast, position + new Vector3(0f, 1f, 0f) + direction * 0.8f, rotation);
        }

        void PlayHit(DamageEvent hit)
        {
            if (hit.Outcome == HitOutcome.Dodged || hit.Outcome == HitOutcome.Absorbed || hit.Outcome == HitOutcome.Interrupted || hit.Outcome == HitOutcome.Stunned)
            {
                return;
            }

            if (hit.Outcome == HitOutcome.Healed)
            {
                if (AbilityCatalog.TryGet(hit.Ability, out var healing) && views.TryGetPosition(hit.Target, out var healed))
                {
                    var healEffect = healing.TryGetVariant(hit.Variant, out var healVariant) && healVariant.hitEffect != null ? healVariant.hitEffect : healing.hitEffect;
                    if (healEffect != null)
                    {
                        EffectPlayer.Play(healEffect, healed + new Vector3(0f, 1f, 0f), Quaternion.identity);
                    }
                }

                return;
            }

            views.OnHit(hit.Target);
            if (AbilityCatalog.TryGet(hit.Ability, out var ability) && views.TryGetPosition(hit.Target, out var position))
            {
                var effect = ability.TryGetVariant(hit.Variant, out var variant) && variant.hitEffect != null ? variant.hitEffect : ability.hitEffect;
                if (effect != null)
                {
                    EffectPlayer.Play(effect, position + new Vector3(0f, 1f, 0f), Quaternion.identity);
                }
            }
        }

        void CycleSkin()
        {
            var className = HudSettings.ClassName;
            var skins = SkinCatalog.ForClass(className);
            if (skins.Count == 0)
            {
                return;
            }

            var current = HudSettings.SkinFor(className);
            var index = 0;
            for (var i = 0; i < skins.Count; i++)
            {
                if (skins[i].name == current)
                {
                    index = (i + 1) % skins.Count;
                    break;
                }
            }

            HudSettings.SetSkin(className, skins[index].name);
            Reload();
        }

        void ToggleCharacter()
        {
            var open = !characterScreen.IsOpen;
            characterScreen.SetOpen(open);
            if (open)
            {
                skillPanel.SetOpen(false);
                menu.SetOpen(false);
                RefreshCharacter();
            }
        }

        void RefreshCharacter()
        {
            var weaponClass = classDefs.TryGet(mastery.Profile.EquippedType, out var def) ? def : playerClass;
            characterScreen.Show(CharacterInfo.Build(mastery, weaponClass, abilityDefs, loadout), mastery);
        }

        // Carrying another weapon or set restarts the session: the kit, skills and numbers are sent when it begins (hub-only later).
        void Equip(CharacterScreen.Tab tab, string uid)
        {
            if (tab == CharacterScreen.Tab.Weapons)
            {
                var weapon = mastery.Profile.Weapons.Find(w => w.Uid == uid);
                if (weapon != null && mastery.EquipWeapon(uid))
                {
                    HudSettings.ClassName = weapon.Type;
                }
            }
            else
            {
                mastery.EquipArmor(uid);
            }

            Reload();
        }

        // Taking something apart does not change what is in use, so the session carries on and the screen just updates.
        void TakeApart(CharacterScreen.Tab tab, string uid)
        {
            var gold = 0;
            var done = tab == CharacterScreen.Tab.Weapons ? mastery.DismantleWeapon(uid, out gold) : mastery.DismantleArmor(uid, out gold);
            if (done)
            {
                RefreshCharacter();
            }
        }

        static void Reload()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void ChangeZoom(int direction)
        {
            HudSettings.CameraZoom -= direction * HudSettings.ZoomStep;
        }

        void RequestSwapSides()
        {
            swapRequested = true;
        }

        static Vector2 KeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }
    }
}
