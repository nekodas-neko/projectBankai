using System.Collections.Generic;
using UnityEngine;

namespace Game.Client
{
    public sealed class Telegraphs
    {
        const float Height = 0.05f;
        const float FillHeight = 0.07f;

        readonly Transform parent;
        readonly Mesh cube;
        readonly Material area;
        readonly Material fill;
        readonly List<Marker> active = new List<Marker>();
        readonly Stack<Marker> pool = new Stack<Marker>();

        public Telegraphs(Transform parent, Mesh cube, Material area, Material fill)
        {
            this.parent = parent;
            this.cube = cube;
            this.area = area;
            this.fill = fill;
        }

        public int ActiveCount => active.Count;

        public void Add(Vector3 origin, Vector2 worldDirection, float range, float radius, float seconds)
        {
            if (worldDirection.sqrMagnitude < 1e-6f || seconds <= 0f)
            {
                return;
            }

            var marker = pool.Count > 0 ? pool.Pop() : Create();
            marker.Origin = origin;
            marker.Direction = new Vector3(worldDirection.x, 0f, worldDirection.y).normalized;
            marker.Range = range;
            marker.Width = Mathf.Max(0.1f, radius * 2f);
            marker.Seconds = seconds;
            marker.Elapsed = 0f;
            marker.Area.gameObject.SetActive(true);
            marker.Fill.gameObject.SetActive(true);
            active.Add(marker);
            Place(marker);
        }

        public void Tick(float dt)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var marker = active[i];
                marker.Elapsed += dt;
                if (marker.Elapsed >= marker.Seconds)
                {
                    marker.Area.gameObject.SetActive(false);
                    marker.Fill.gameObject.SetActive(false);
                    active.RemoveAt(i);
                    pool.Push(marker);
                    continue;
                }

                Place(marker);
            }
        }

        public void Clear()
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var marker = active[i];
                marker.Area.gameObject.SetActive(false);
                marker.Fill.gameObject.SetActive(false);
                pool.Push(marker);
            }

            active.Clear();
        }

        Marker Create()
        {
            return new Marker { Area = NewPart("TelegraphArea", area), Fill = NewPart("TelegraphFill", fill) };
        }

        Transform NewPart(string name, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = cube;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            return go.transform;
        }

        static void Place(Marker marker)
        {
            var rotation = Quaternion.LookRotation(marker.Direction, Vector3.up);
            marker.Area.SetPositionAndRotation(marker.Origin + marker.Direction * (marker.Range * 0.5f) + new Vector3(0f, Height, 0f), rotation);
            marker.Area.localScale = new Vector3(marker.Width, 0.02f, marker.Range);

            var progress = Mathf.Clamp01(marker.Elapsed / marker.Seconds);
            var length = Mathf.Max(0.01f, marker.Range * progress);
            marker.Fill.SetPositionAndRotation(marker.Origin + marker.Direction * (length * 0.5f) + new Vector3(0f, FillHeight, 0f), rotation);
            marker.Fill.localScale = new Vector3(marker.Width * 0.96f, 0.02f, length);
        }

        sealed class Marker
        {
            public Transform Area;
            public Transform Fill;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Range;
            public float Width;
            public float Seconds;
            public float Elapsed;
        }
    }
}
