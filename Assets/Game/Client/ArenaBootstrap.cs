using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    public class ArenaBootstrap : MonoBehaviour
    {
        const string SlimeClass = "slime";
        const string BossClass = "slime_boss";
        static readonly string[] StarterClasses = { "tank", "healer", "mage" };
        const float MaxFrameSeconds = 0.1f;
        const float CameraSmoothing = 8f;

        public ArenaAssets assets;
        static readonly Vector3 CameraOffset = new Vector3(0f, 26f, -12f);

        readonly List<DamageEvent> events = new List<DamageEvent>();
        readonly List<UseEvent> uses = new List<UseEvent>();
        LocalSession session;
        EntityViews views;
        AimIndicator aimIndicator;
        ClassDef playerClass;
        Registry<AbilityDef> abilityDefs;
        Camera viewCamera;
        bool swapRequested;

        public LocalSession Session => session;

        public GameClient LocalClient { get; private set; }

        public Hud Hud { get; private set; }

        public EntityViews Views => views;

        void Start()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            session = new LocalSession(new ServerConfig(), classes, abilities, seed: 1);
            var className = HudSettings.ClassName;
            LocalClient = session.AddPlayer(new PlayerId(1), className, StableId.From(HudSettings.SkinFor(className)));
            var slime = StableId.From("slime_green");
            session.Server.SpawnNpc(SlimeClass, Team.Enemies, new Vec2(0f, 8f), slime);
            session.Server.SpawnNpc(SlimeClass, Team.Enemies, new Vec2(-8f, 4f), slime);
            session.Server.SpawnNpc(SlimeClass, Team.Enemies, new Vec2(7f, -2f), slime);
            session.Server.SpawnNpc(BossClass, Team.Enemies, new Vec2(0f, 15f), StableId.From("slime_king"));

            playerClass = classes.Get(className);
            abilityDefs = abilities;
            views = new EntityViews(transform, assets);
            aimIndicator = new AimIndicator(transform, assets.cube, assets.aim);
            Hud = Hud.Create(HudSettings.MoveOnRight, RequestSwapSides, ChangeZoom, CycleSkin, CycleClass);
            WireButtons();
            viewCamera = Camera.main;
        }

        void Update()
        {
            if (session == null)
            {
                return;
            }

            if (swapRequested)
            {
                swapRequested = false;
                HudSettings.MoveOnRight = !HudSettings.MoveOnRight;
                Destroy(Hud.Root);
                Hud = Hud.Create(HudSettings.MoveOnRight, RequestSwapSides, ChangeZoom, CycleSkin, CycleClass);
                WireButtons();
            }

            var dt = Mathf.Min(Time.deltaTime, MaxFrameSeconds);
            var input = Hud.MoveStick.Value;
            if (input == Vector2.zero)
            {
                input = KeyboardInput();
            }

            LocalClient.SetMoveInput(new Vec2(input.x, input.y));
            session.Advance(dt);
            views.Sync(LocalClient, dt);

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

        void WireButtons()
        {
            Hud.Attack.Fired += aim => LocalClient.Attack(ToWorld(aim));
            Hud.Special.Fired += aim => LocalClient.Skill(1, ToWorld(aim));
        }

        void UpdateButtons()
        {
            var hasState = LocalClient.TryGetLocalState(out var state);
            UpdateButton(Hud.Attack, hasState, state, 0, playerClass.AutoAttack);
            UpdateButton(Hud.Special, hasState, state, 1, playerClass.Skills.Length > 0 ? playerClass.Skills[0] : null);

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
                aimIndicator.Show(views.LocalRoot.position, new Vector2(world.X, world.Y), ability.Range, ability.Radius);
            }
        }

        void UpdateButton(ActionButton button, bool hasState, EntityState state, int slot, string abilityId)
        {
            if (!hasState || !abilityDefs.TryGet(abilityId, out var ability) || ability.Cooldown <= 0f)
            {
                button.SetCooldown(0f);
                return;
            }

            button.SetCooldown(state.GetCooldown(slot) / ability.Cooldown);
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

        void PlayUse(UseEvent use)
        {
            views.OnUse(use.User, use.Slot);
            if (!AbilityCatalog.TryGet(use.Ability, out var ability) || ability.castEffect == null || !views.TryGetPosition(use.User, out var position))
            {
                return;
            }

            var direction = new Vector3(use.Direction.X, 0f, use.Direction.Y);
            var rotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            EffectPlayer.Play(ability.castEffect, position + new Vector3(0f, 1f, 0f) + direction * 0.8f, rotation);
        }

        void PlayHit(DamageEvent hit)
        {
            views.OnHit(hit.Target);
            if (AbilityCatalog.TryGet(hit.Ability, out var ability) && ability.hitEffect != null && views.TryGetPosition(hit.Target, out var position))
            {
                EffectPlayer.Play(ability.hitEffect, position + new Vector3(0f, 1f, 0f), Quaternion.identity);
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
