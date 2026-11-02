using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using EntityId = Game.Core.EntityId;

namespace Game.Client
{
    [Serializable]
    public class ArenaAssets
    {
        public Mesh capsule;
        public Mesh cube;
        public Material player;
        public Material ally;
        public Material enemy;
        public Material dead;
        public Material flash;
        public Material barBackground;
        public Material aim;
    }

    public sealed class EntityViews
    {
        const float FlashSeconds = 0.12f;
        const float BarWidth = 1.2f;
        const float LocalSmoothing = 25f;

        readonly Transform parent;
        readonly ArenaAssets assets;
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<int> stale = new List<int>();
        readonly HashSet<int> seen = new HashSet<int>();

        public EntityViews(Transform parent, ArenaAssets assets)
        {
            this.parent = parent;
            this.assets = assets;
        }

        public Transform LocalRoot { get; private set; }

        public int Count => views.Count;

        public bool TryGetPosition(EntityId id, out Vector3 position)
        {
            if (views.TryGetValue(id.Value, out var view))
            {
                position = view.Root.position;
                return true;
            }

            position = default;
            return false;
        }

        public void Flash(EntityId id)
        {
            if (views.TryGetValue(id.Value, out var view))
            {
                view.FlashRemaining = FlashSeconds;
            }
        }

        public void Sync(GameClient client, float dt)
        {
            var snapshot = client.Latest;
            if (snapshot == null)
            {
                return;
            }

            seen.Clear();
            for (var i = 0; i < snapshot.Entities.Count; i++)
            {
                var state = snapshot.Entities[i];
                seen.Add(state.Id.Value);
                var isLocal = state.Id == client.LocalEntity;

                Vec2 position;
                if (isLocal)
                {
                    position = client.PredictedPosition;
                }
                else if (!client.TrySampleRemote(state.Id, out position))
                {
                    position = state.Position;
                }

                var target = new Vector3(position.X, 0f, position.Y);
                if (!views.TryGetValue(state.Id.Value, out var view))
                {
                    view = Create(state);
                    view.Root.position = target;
                    views.Add(state.Id.Value, view);
                }
                else if (isLocal)
                {
                    view.Root.position = Vector3.Lerp(view.Root.position, target, 1f - Mathf.Exp(-LocalSmoothing * dt));
                }
                else
                {
                    view.Root.position = target;
                }

                if (isLocal)
                {
                    LocalRoot = view.Root;
                }

                Apply(view, state, client.Player, dt);
            }

            stale.Clear();
            foreach (var pair in views)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                UnityEngine.Object.Destroy(views[stale[i]].Root.gameObject);
                views.Remove(stale[i]);
            }
        }

        void Apply(View view, EntityState state, PlayerId localPlayer, float dt)
        {
            if (state.Facing.SqrLength > 1e-6f)
            {
                var yaw = Mathf.Atan2(state.Facing.X, state.Facing.Y) * Mathf.Rad2Deg;
                view.Body.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            var alive = state.Health > 0f;
            view.FlashRemaining = Mathf.Max(0f, view.FlashRemaining - dt);

            Material material;
            if (!alive)
            {
                material = assets.dead;
            }
            else if (view.FlashRemaining > 0f)
            {
                material = assets.flash;
            }
            else if (state.Owner == localPlayer)
            {
                material = assets.player;
            }
            else
            {
                material = state.Team == Team.Enemies ? assets.enemy : assets.ally;
            }

            view.BodyRenderer.sharedMaterial = material;
            view.NoseRenderer.sharedMaterial = material;

            var fraction = state.MaxHealth > 0f ? Mathf.Clamp01(state.Health / state.MaxHealth) : 0f;
            view.BarBackground.gameObject.SetActive(alive);
            view.BarFill.gameObject.SetActive(alive);
            view.BarFill.localScale = new Vector3(BarWidth * fraction, 0.12f, 0.05f);
            view.BarFill.localPosition = new Vector3(-BarWidth * 0.5f * (1f - fraction), 2.6f, -0.04f);
        }

        View Create(EntityState state)
        {
            var root = new GameObject($"Entity {state.Id.Value}").transform;
            root.SetParent(parent, false);

            var body = Part(assets.capsule, root, "Body");
            body.localPosition = new Vector3(0f, 1f, 0f);

            var nose = Part(assets.cube, body, "Nose");
            nose.localScale = new Vector3(0.3f, 0.3f, 0.6f);
            nose.localPosition = new Vector3(0f, 0.35f, 0.55f);

            var background = Part(assets.cube, root, "BarBackground");
            background.localScale = new Vector3(BarWidth + 0.08f, 0.18f, 0.05f);
            background.localPosition = new Vector3(0f, 2.6f, 0f);
            background.GetComponent<Renderer>().sharedMaterial = assets.barBackground;

            var fill = Part(assets.cube, root, "BarFill");
            var fillRenderer = fill.GetComponent<Renderer>();
            fillRenderer.sharedMaterial = state.Team == Team.Enemies ? assets.enemy : assets.player;

            return new View
            {
                Root = root,
                Body = body,
                BodyRenderer = body.GetComponent<Renderer>(),
                NoseRenderer = nose.GetComponent<Renderer>(),
                BarBackground = background,
                BarFill = fill,
            };
        }

        static Transform Part(Mesh mesh, Transform parent, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        sealed class View
        {
            public Transform Root;
            public Transform Body;
            public Renderer BodyRenderer;
            public Renderer NoseRenderer;
            public Transform BarBackground;
            public Transform BarFill;
            public float FlashRemaining;
        }
    }
}
