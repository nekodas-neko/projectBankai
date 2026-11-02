using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    public class ArenaBootstrap : MonoBehaviour
    {
        const string PlayerClass = "fighter";
        const string DummyClass = "training_dummy";
        const float MaxFrameSeconds = 0.1f;
        const float CameraSmoothing = 8f;

        public ArenaAssets assets;
        public Vector3 cameraOffset = new Vector3(0f, 26f, -15f);

        readonly List<DamageEvent> events = new List<DamageEvent>();
        LocalSession session;
        EntityViews views;
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
            LocalClient = session.AddPlayer(new PlayerId(1), PlayerClass);
            session.Server.SpawnNpc(DummyClass, Team.Enemies, new Vec2(0f, 8f));
            session.Server.SpawnNpc(DummyClass, Team.Enemies, new Vec2(-8f, 4f));
            session.Server.SpawnNpc(DummyClass, Team.Enemies, new Vec2(7f, -2f));

            views = new EntityViews(transform, assets);
            Hud = Hud.Create(HudSettings.MoveOnRight, RequestSwapSides);
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
                Hud = Hud.Create(HudSettings.MoveOnRight, RequestSwapSides);
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

            events.Clear();
            LocalClient.DrainEvents(events);
            for (var i = 0; i < events.Count; i++)
            {
                views.Flash(events[i].Target);
            }
        }

        void LateUpdate()
        {
            if (viewCamera == null || views == null || views.LocalRoot == null)
            {
                return;
            }

            var target = views.LocalRoot.position + cameraOffset;
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, target, 1f - Mathf.Exp(-CameraSmoothing * Time.deltaTime));
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
