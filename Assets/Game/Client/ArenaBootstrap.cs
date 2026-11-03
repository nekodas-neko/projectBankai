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

        static readonly string[] StarterClasses = { "sword_and_shield", "healing_mace", "staff" };
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
        ClassDef playerClass;
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
            loadout = Loadout(className);
            views = new EntityViews(transform, assets);
            aimIndicator = new AimIndicator(transform, assets.cube, assets.aim);
            telegraphs = new Telegraphs(transform, assets.cube, assets.cylinder, assets.telegraph, assets.telegraphFill);
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

        void OnDestroy()
        {
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
                    LocalClient = new GameClient(transport.PlayerId, transport, playerClass, new ClientConfig { Simulation = transport.Simulation });
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
            SyncTelegraphs();
            UpdateButtons();

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
            LocalClient = session.AddPlayer(new PlayerId(1), className, StableId.From(HudSettings.SkinFor(className)), loadout);
            session.Server.StartDungeon(dungeonDefs.Get(DungeonId));
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
                transport = new UdpClientTransport(endpoint, className, StableId.From(HudSettings.SkinFor(className)), DeviceName(), loadout);
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
                CycleClass = CycleClass,
                CycleStyle = CycleStyle,
                ToggleSession = ToggleMenu,
                Restart = () => LocalClient?.Restart(),
            });

            menu = new SessionMenu(
                (RectTransform)Hud.Root.transform,
                () => SwitchMode(SessionMode.Solo, null),
                () => SwitchMode(SessionMode.Host, null),
                address => SwitchMode(SessionMode.Join, address),
                ToggleMenu);
            WireButtons();
        }

        void ToggleMenu()
        {
            var open = !menu.IsOpen;
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
            Hud.Attack.Fired += aim => LocalClient?.Attack(ToWorld(aim));
            Hud.Special.Fired += aim => LocalClient?.Skill(1, ToWorld(aim));
            Hud.Ultimate.Fired += aim => LocalClient?.Skill(Entity.UltimateSlot, ToWorld(aim));
            Hud.Defense.Fired += aim => LocalClient?.Defend(DefendDirection(aim));
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
            Hud.Banner.Update(LocalClient.Latest, hasState && state.Health <= 0f);

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

        int[] Loadout(string className)
        {
            var def = classDefs.Get(className);
            var style = HudSettings.StyleFor(className);
            return new[] { StyleVariant(def.AutoAttack, style), StyleVariant(def.Skills.Length > 0 ? def.Skills[0] : null, style) };
        }

        int StyleVariant(string abilityId, int style)
        {
            if (style <= 0 || !abilityDefs.TryGet(abilityId, out var ability) || ability.Variants.Length < style)
            {
                return 0;
            }

            return StableId.From(ability.Variants[style - 1].Id);
        }

        int StyleCount(string className)
        {
            var def = classDefs.Get(className);
            var most = 0;
            foreach (var id in new[] { def.AutoAttack, def.Skills.Length > 0 ? def.Skills[0] : null })
            {
                if (abilityDefs.TryGet(id, out var ability))
                {
                    most = Mathf.Max(most, ability.Variants.Length);
                }
            }

            return 1 + most;
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
            if (hit.Outcome == HitOutcome.Dodged || hit.Outcome == HitOutcome.Absorbed)
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

        void CycleStyle()
        {
            var className = HudSettings.ClassName;
            HudSettings.SetStyle(className, (HudSettings.StyleFor(className) + 1) % StyleCount(className));
            Reload();
        }

        void CycleClass()
        {
            var index = (System.Array.IndexOf(StarterClasses, HudSettings.ClassName) + 1) % StarterClasses.Length;
            HudSettings.ClassName = StarterClasses[index];
            Reload();
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
