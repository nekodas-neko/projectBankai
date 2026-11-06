using UnityEngine;

namespace Game.Client
{
    public sealed class CharacterVisual
    {
        const float PunchSeconds = 0.22f;
        const float HitSeconds = 0.14f;
        const float BobRate = 11f;

        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int UseParameter = Animator.StringToHash("Use");
        static readonly int SlotParameter = Animator.StringToHash("Slot");
        static readonly int HitParameter = Animator.StringToHash("Hit");
        static readonly int DeadParameter = Animator.StringToHash("Dead");

        readonly Renderer[] renderers;
        readonly Material[][] originals;
        readonly Animator animator;
        readonly Vector3 baseScale;
        readonly Vector3 basePosition;
        readonly float bobHeight;
        float punch;
        float hit;
        float clock;
        bool blinking;

        CharacterVisual(Transform body, Renderer[] renderers, Animator animator, Vector3 baseScale, Vector3 basePosition, float bobHeight)
        {
            this.bobHeight = bobHeight;
            Body = body;
            this.renderers = renderers;
            this.animator = animator;
            this.baseScale = baseScale;
            this.basePosition = basePosition;

            originals = new Material[renderers.Length][];
            for (var i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].sharedMaterials;
            }
        }

        public Transform Body { get; }

        public Material BodyMaterial => renderers.Length > 0 ? originals[0][0] : null;

        public static CharacterVisual Create(Transform parent, SkinAsset skin, ArenaAssets assets)
        {
            if (skin != null && skin.prefab != null)
            {
                var instance = Object.Instantiate(skin.prefab, parent);
                instance.name = "Body";
                instance.transform.localPosition = Vector3.zero;
                return new CharacterVisual(
                    instance.transform,
                    instance.GetComponentsInChildren<Renderer>(),
                    instance.GetComponentInChildren<Animator>(),
                    instance.transform.localScale,
                    Vector3.zero,
                    0f);
            }

            var sphere = skin != null && skin.shape == SkinAsset.BodyShape.Sphere;
            var body = Part(sphere ? assets.sphere : assets.capsule, parent, "Body");
            var scale = skin != null ? skin.scale : Vector3.one;
            body.localScale = scale;
            body.localPosition = new Vector3(0f, sphere ? scale.y * 0.5f : scale.y, 0f);
            var nose = Part(assets.cube, body, "Nose");
            nose.localScale = new Vector3(0.3f, 0.3f, 0.6f);
            nose.localPosition = sphere ? new Vector3(0f, 0.15f, 0.5f) : new Vector3(0f, 0.35f, 0.55f);

            var bodyRenderer = body.GetComponent<Renderer>();
            var noseRenderer = nose.GetComponent<Renderer>();
            if (skin != null)
            {
                bodyRenderer.sharedMaterial = skin.body;
                noseRenderer.sharedMaterial = skin.accent != null ? skin.accent : skin.body;
            }

            return new CharacterVisual(body, new[] { bodyRenderer, noseRenderer }, null, scale, body.localPosition, skin != null ? skin.bobHeight : 0.14f);
        }

        public static Transform Part(Mesh mesh, Transform parent, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        public void SetFacing(Vector2 facing)
        {
            if (facing.sqrMagnitude > 1e-6f)
            {
                Body.rotation = Quaternion.Euler(0f, Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg, 0f);
            }
        }

        public void OnUse(int slot)
        {
            punch = PunchSeconds;
            if (animator != null)
            {
                animator.SetInteger(SlotParameter, slot);
                animator.SetTrigger(UseParameter);
            }
        }

        public void OnHit()
        {
            hit = HitSeconds;
            if (animator != null)
            {
                animator.SetTrigger(HitParameter);
            }
        }

        public void Tick(float dt, float speed, bool dead)
        {
            clock += dt;
            punch = Mathf.Max(0f, punch - dt);
            hit = Mathf.Max(0f, hit - dt);

            if (animator != null)
            {
                animator.SetFloat(SpeedParameter, speed);
                animator.SetBool(DeadParameter, dead);
                return;
            }

            if (dead)
            {
                Body.localPosition = basePosition * 0.4f;
                Body.localScale = baseScale;
                var yaw = Body.rotation.eulerAngles.y;
                Body.rotation = Quaternion.Euler(90f, yaw, 0f);
                return;
            }

            var idle = bobHeight * 0.25f * (1f + Mathf.Sin(clock * 3f)) * 0.5f;
            var bob = idle + Mathf.Abs(Mathf.Sin(clock * BobRate)) * bobHeight * Mathf.Clamp01(speed / 3f);
            Body.localPosition = basePosition + new Vector3(0f, bob, 0f);

            var pulse = 1f + 0.28f * Mathf.Sin(Mathf.PI * (punch / PunchSeconds));
            var squash = hit > 0f ? 1f - 0.18f * Mathf.Sin(Mathf.PI * (hit / HitSeconds)) : 1f;
            Body.localScale = new Vector3(baseScale.x * pulse / squash, baseScale.y * pulse * squash, baseScale.z * pulse / squash);
        }

        public void SetBlink(bool on)
        {
            if (!on && !blinking)
            {
                return;
            }

            blinking = on;
            var visible = !on || Mathf.FloorToInt(clock * 18f) % 2 == 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        public void SetFlash(Material flash, bool on)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!on)
                {
                    renderers[i].sharedMaterials = originals[i];
                    continue;
                }

                var flashed = new Material[originals[i].Length];
                for (var j = 0; j < flashed.Length; j++)
                {
                    flashed[j] = flash;
                }

                renderers[i].sharedMaterials = flashed;
            }
        }
    }
}
