using UnityEngine;

namespace Game.Client
{
    // A pulsing ring on the ground under whoever the boss is fighting.
    public sealed class BossTargetMarker
    {
        const float Height = 0.08f;
        const float Size = 2.2f;
        const float PulseSize = 0.25f;
        const float PulseSpeed = 5f;

        readonly GameObject root;

        public BossTargetMarker(Transform parent, Mesh cylinder, Material source)
        {
            root = new GameObject("BossTargetMarker", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            root.GetComponent<MeshFilter>().sharedMesh = cylinder;
            if (source != null)
            {
                var material = new Material(source);
                var color = Telegraphs.Tint(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color, BossFrame.OnYou);
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else
                {
                    material.color = color;
                }

                root.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            root.SetActive(false);
        }

        public bool IsVisible => root.activeSelf;

        public Vector3 Position => root.transform.position;

        public void Show(Vector3 position, float time)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            var size = Size + Mathf.Sin(time * PulseSpeed) * PulseSize;
            root.transform.position = new Vector3(position.x, Height, position.z);
            root.transform.localScale = new Vector3(size, 0.01f, size);
        }

        public void Hide()
        {
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }
    }
}
