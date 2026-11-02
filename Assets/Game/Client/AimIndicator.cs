using UnityEngine;

namespace Game.Client
{
    public sealed class AimIndicator
    {
        const float Height = 0.06f;

        readonly Transform line;

        public AimIndicator(Transform parent, Mesh cube, Material material)
        {
            var go = new GameObject("AimIndicator", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = cube;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            line = go.transform;
            line.SetParent(parent, false);
            go.SetActive(false);
        }

        public void Show(Vector3 origin, Vector2 worldDirection, float range, float radius)
        {
            if (worldDirection.sqrMagnitude < 1e-6f)
            {
                Hide();
                return;
            }

            var direction = new Vector3(worldDirection.x, 0f, worldDirection.y).normalized;
            line.gameObject.SetActive(true);
            line.position = origin + direction * (range * 0.5f) + new Vector3(0f, Height, 0f);
            line.rotation = Quaternion.LookRotation(direction, Vector3.up);
            line.localScale = new Vector3(Mathf.Max(0.1f, radius * 2f), 0.04f, range);
        }

        public void Hide()
        {
            if (line.gameObject.activeSelf)
            {
                line.gameObject.SetActive(false);
            }
        }
    }
}
