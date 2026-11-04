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
        public Mesh sphere;
        public Mesh cube;
        public Mesh cylinder;
        public Material player;
        public Material ally;
        public Material enemy;
        public Material dead;
        public Material flash;
        public Material barBackground;
        public Material aim;
        public Material telegraph;
        public Material telegraphFill;
    }

    public sealed class EntityViews
    {
        const float FlashSeconds = 0.12f;
        const float BarWidth = 1.2f;
        const float BossBarScale = 2.2f;
        static readonly Color CastColor = new Color(0.4f, 0.7f, 1f);
        const float LocalSmoothing = 25f;
        const float SpeedSmoothing = 12f;

        readonly Transform parent;
        readonly ArenaAssets assets;
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<int> stale = new List<int>();
        readonly HashSet<int> seen = new HashSet<int>();
        Material castMaterial;

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

        public bool TryGetBodyMaterial(EntityId id, out Material material)
        {
            if (views.TryGetValue(id.Value, out var view))
            {
                material = view.Visual.BodyMaterial;
                return true;
            }

            material = null;
            return false;
        }

        // A bar above an enemy's health bar that fills while it winds up a cast; a negative fraction hides it.
        public void SetCast(EntityId id, float fraction)
        {
            if (!views.TryGetValue(id.Value, out var view))
            {
                return;
            }

            if (fraction < 0f)
            {
                if (view.CastBack != null && view.CastBack.gameObject.activeSelf)
                {
                    view.CastBack.gameObject.SetActive(false);
                    view.CastFill.gameObject.SetActive(false);
                }

                return;
            }

            if (view.CastBack == null)
            {
                castMaterial = castMaterial != null ? castMaterial : Telegraphs.Tinted(assets.barBackground, CastColor);
                view.CastBack = CharacterVisual.Part(assets.cube, view.Root, "CastBackground");
                view.CastBack.localScale = new Vector3(view.CastWidth + 0.1f, 0.26f * view.BarScale, 0.05f);
                view.CastBack.localPosition = new Vector3(0f, view.BarHeight + 0.3f * view.BarScale, 0f);
                view.CastBack.GetComponent<Renderer>().sharedMaterial = assets.barBackground;
                view.CastFill = CharacterVisual.Part(assets.cube, view.Root, "CastFill");
                view.CastFill.GetComponent<Renderer>().sharedMaterial = castMaterial != null ? castMaterial : assets.player;
            }

            fraction = Mathf.Clamp01(fraction);
            view.CastBack.gameObject.SetActive(true);
            view.CastFill.gameObject.SetActive(true);
            view.CastFill.localScale = new Vector3(Mathf.Max(0.001f, view.CastWidth * fraction), 0.18f * view.BarScale, 0.05f);
            view.CastFill.localPosition = new Vector3(-view.CastWidth * 0.5f * (1f - fraction), view.BarHeight + 0.3f * view.BarScale, -0.04f);
        }

        public bool CastVisible(EntityId id)
        {
            return views.TryGetValue(id.Value, out var view) && view.CastBack != null && view.CastBack.gameObject.activeSelf;
        }

        public void OnUse(EntityId id, int slot)
        {
            if (views.TryGetValue(id.Value, out var view))
            {
                view.Visual.OnUse(slot);
            }
        }

        public void OnHit(EntityId id)
        {
            if (views.TryGetValue(id.Value, out var view))
            {
                view.FlashRemaining = FlashSeconds;
                view.Visual.OnHit();
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
                    view = Create(state, client.Player);
                    view.Root.position = target;
                    view.LastPosition = target;
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

                Apply(view, state, dt);
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

        void Apply(View view, EntityState state, float dt)
        {
            var moved = (view.Root.position - view.LastPosition).magnitude / Mathf.Max(dt, 1e-4f);
            view.LastPosition = view.Root.position;
            view.Speed = Mathf.Lerp(view.Speed, moved, 1f - Mathf.Exp(-SpeedSmoothing * dt));

            var alive = state.Health > 0f;
            view.Visual.SetBlink(alive && (state.Effects & EffectFlags.Invulnerable) != 0);
            view.Visual.SetFacing(new Vector2(state.Facing.X, state.Facing.Y));
            view.Visual.Tick(dt, view.Speed, !alive);

            view.FlashRemaining = Mathf.Max(0f, view.FlashRemaining - dt);
            var flashing = alive && view.FlashRemaining > 0f;
            if (flashing != view.Flashing)
            {
                view.Flashing = flashing;
                view.Visual.SetFlash(assets.flash, flashing);
            }

            view.Ring.gameObject.SetActive(alive);
            var guarded = (state.Effects & (EffectFlags.Blocking | EffectFlags.Shielded)) != 0;
            var ringScale = view.RingSize * (guarded ? 1.45f : 1f);
            view.Ring.localScale = new Vector3(ringScale, 0.02f, ringScale);
            view.BarBackground.gameObject.SetActive(alive);
            view.BarFill.gameObject.SetActive(alive);
            if (!alive && view.CastBack != null && view.CastBack.gameObject.activeSelf)
            {
                view.CastBack.gameObject.SetActive(false);
                view.CastFill.gameObject.SetActive(false);
            }

            var fraction = state.MaxHealth > 0f ? Mathf.Clamp01(state.Health / state.MaxHealth) : 0f;
            var width = BarWidth * view.BarScale;
            view.BarFill.localScale = new Vector3(width * fraction, 0.12f * view.BarScale, 0.05f);
            view.BarFill.localPosition = new Vector3(-width * 0.5f * (1f - fraction), view.BarHeight, -0.04f);
        }

        View Create(EntityState state, PlayerId localPlayer)
        {
            var root = new GameObject($"Entity {state.Id.Value}").transform;
            root.SetParent(parent, false);

            var teamMaterial = state.Owner == localPlayer ? assets.player : state.Team == Team.Enemies ? assets.enemy : assets.ally;
            var skin = SkinCatalog.Get(state.Appearance);
            var scale = skin != null ? skin.scale : Vector3.one;
            var barHeight = 1.6f + 1f * scale.y;

            var ring = CharacterVisual.Part(assets.cylinder, root, "TeamRing");
            var ringSize = 1.7f * Mathf.Max(scale.x, scale.z);
            ring.localScale = new Vector3(ringSize, 0.02f, ringSize);
            ring.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.GetComponent<Renderer>().sharedMaterial = teamMaterial;

            var visual = CharacterVisual.Create(root, skin, assets);

            var background = CharacterVisual.Part(assets.cube, root, "BarBackground");
            var barScale = (state.Effects & EffectFlags.Boss) != 0 ? BossBarScale : 1f;
            background.localScale = new Vector3(BarWidth * barScale + 0.08f, 0.18f * barScale, 0.05f);
            background.localPosition = new Vector3(0f, barHeight, 0f);
            background.GetComponent<Renderer>().sharedMaterial = assets.barBackground;

            var fill = CharacterVisual.Part(assets.cube, root, "BarFill");
            fill.GetComponent<Renderer>().sharedMaterial = teamMaterial;

            return new View { Root = root, Visual = visual, Ring = ring, RingSize = ringSize, BarBackground = background, BarFill = fill, BarHeight = barHeight, BarScale = barScale };
        }

        sealed class View
        {
            public Transform Root;
            public CharacterVisual Visual;
            public Transform Ring;
            public float RingSize;
            public Transform BarBackground;
            public Transform BarFill;
            public Transform CastBack;
            public Transform CastFill;
            public float BarHeight;
            public float BarScale = 1f;
            public float CastWidth => Mathf.Max(1.8f, BarWidth * BarScale);
            public Vector3 LastPosition;
            public float Speed;
            public float FlashRemaining;
            public bool Flashing;
        }
    }
}
