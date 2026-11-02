using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    public sealed class Telegraphs
    {
        const float Height = 0.05f;
        const float FillHeight = 0.07f;
        const int ConeSegments = 16;

        readonly Transform parent;
        readonly Mesh cube;
        readonly Mesh cylinder;
        readonly Material area;
        readonly Material fill;
        readonly List<Marker> active = new List<Marker>();
        readonly Stack<Marker> pool = new Stack<Marker>();
        readonly Dictionary<int, Mesh> wedges = new Dictionary<int, Mesh>();

        public Telegraphs(Transform parent, Mesh cube, Mesh cylinder, Material area, Material fill)
        {
            this.parent = parent;
            this.cube = cube;
            this.cylinder = cylinder;
            this.area = area;
            this.fill = fill;
        }

        public int ActiveCount => active.Count;

        public void Add(Vector3 origin, Vector2 worldDirection, AbilityShape shape, float range, float radius, float angle, float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            if (worldDirection.sqrMagnitude < 1e-6f)
            {
                if (shape != AbilityShape.Circle)
                {
                    return;
                }

                worldDirection = Vector2.up;
            }

            var marker = pool.Count > 0 ? pool.Pop() : Create();
            marker.Shape = shape;
            marker.Origin = origin;
            marker.Direction = new Vector3(worldDirection.x, 0f, worldDirection.y).normalized;
            marker.Range = range;
            marker.Radius = radius;
            marker.Seconds = seconds;
            marker.Elapsed = 0f;

            var mesh = shape == AbilityShape.Circle ? cylinder : shape == AbilityShape.Cone ? Wedge(angle) : cube;
            marker.AreaFilter.sharedMesh = mesh;
            marker.FillFilter.sharedMesh = mesh;
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
                    Retire(marker);
                    active.RemoveAt(i);
                    continue;
                }

                Place(marker);
            }
        }

        public void Clear()
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                Retire(active[i]);
            }

            active.Clear();
        }

        void Retire(Marker marker)
        {
            marker.Area.gameObject.SetActive(false);
            marker.Fill.gameObject.SetActive(false);
            pool.Push(marker);
        }

        Marker Create()
        {
            var marker = new Marker();
            marker.Area = NewPart("TelegraphArea", area, out marker.AreaFilter);
            marker.Fill = NewPart("TelegraphFill", fill, out marker.FillFilter);
            return marker;
        }

        Transform NewPart(string name, Material material, out MeshFilter filter)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            filter = go.GetComponent<MeshFilter>();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            return go.transform;
        }

        Mesh Wedge(float angle)
        {
            var key = Mathf.RoundToInt(angle);
            if (wedges.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var half = Mathf.Clamp(key, 1, 360) * 0.5f * Mathf.Deg2Rad;
            var vertices = new Vector3[ConeSegments + 2];
            vertices[0] = Vector3.zero;
            for (var i = 0; i <= ConeSegments; i++)
            {
                var theta = -half + 2f * half * i / ConeSegments;
                vertices[i + 1] = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            }

            var triangles = new int[ConeSegments * 6];
            for (var i = 0; i < ConeSegments; i++)
            {
                triangles[i * 6] = 0;
                triangles[i * 6 + 1] = i + 2;
                triangles[i * 6 + 2] = i + 1;
                triangles[i * 6 + 3] = 0;
                triangles[i * 6 + 4] = i + 1;
                triangles[i * 6 + 5] = i + 2;
            }

            var mesh = new Mesh { name = $"Wedge{key}", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            wedges.Add(key, mesh);
            return mesh;
        }

        static void Place(Marker marker)
        {
            var rotation = Quaternion.LookRotation(marker.Direction, Vector3.up);
            var progress = Mathf.Clamp01(marker.Elapsed / marker.Seconds);
            switch (marker.Shape)
            {
                case AbilityShape.Circle:
                {
                    var full = marker.Radius * 2f;
                    var inner = Mathf.Max(0.02f, full * progress);
                    marker.Area.SetPositionAndRotation(marker.Origin + new Vector3(0f, Height, 0f), rotation);
                    marker.Area.localScale = new Vector3(full, 0.01f, full);
                    marker.Fill.SetPositionAndRotation(marker.Origin + new Vector3(0f, FillHeight, 0f), rotation);
                    marker.Fill.localScale = new Vector3(inner, 0.01f, inner);
                    break;
                }

                case AbilityShape.Cone:
                {
                    var inner = Mathf.Max(0.02f, marker.Range * progress);
                    marker.Area.SetPositionAndRotation(marker.Origin + new Vector3(0f, Height, 0f), rotation);
                    marker.Area.localScale = new Vector3(marker.Range, 1f, marker.Range);
                    marker.Fill.SetPositionAndRotation(marker.Origin + new Vector3(0f, FillHeight, 0f), rotation);
                    marker.Fill.localScale = new Vector3(inner, 1f, inner);
                    break;
                }

                default:
                {
                    var width = Mathf.Max(0.1f, marker.Radius * 2f);
                    var length = Mathf.Max(0.01f, marker.Range * progress);
                    marker.Area.SetPositionAndRotation(marker.Origin + marker.Direction * (marker.Range * 0.5f) + new Vector3(0f, Height, 0f), rotation);
                    marker.Area.localScale = new Vector3(width, 0.02f, marker.Range);
                    marker.Fill.SetPositionAndRotation(marker.Origin + marker.Direction * (length * 0.5f) + new Vector3(0f, FillHeight, 0f), rotation);
                    marker.Fill.localScale = new Vector3(width * 0.96f, 0.02f, length);
                    break;
                }
            }
        }

        sealed class Marker
        {
            public Transform Area;
            public Transform Fill;
            public MeshFilter AreaFilter;
            public MeshFilter FillFilter;
            public AbilityShape Shape;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Range;
            public float Radius;
            public float Seconds;
            public float Elapsed;
        }
    }
}
