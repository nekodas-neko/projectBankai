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
        // Orange warnings can be blocked or dodged; red ones cannot, so you have to leave the area.
        public static readonly Color OrangeArea = new Color(1f, 0.5f, 0.08f);
        public static readonly Color OrangeFill = new Color(1f, 0.78f, 0.25f);
        public static readonly Color RedArea = new Color(0.85f, 0.08f, 0.08f);
        public static readonly Color RedFill = new Color(1f, 0.28f, 0.22f);

        readonly Material orangeAreaMaterial;
        readonly Material orangeFillMaterial;
        readonly Material redAreaMaterial;
        readonly Material redFillMaterial;
        readonly List<Marker> active = new List<Marker>();
        readonly Stack<Marker> pool = new Stack<Marker>();
        readonly Dictionary<int, Mesh> wedges = new Dictionary<int, Mesh>();
        int stamp;

        public Telegraphs(Transform parent, Mesh cube, Mesh cylinder, Material area, Material fill)
        {
            this.parent = parent;
            this.cube = cube;
            this.cylinder = cylinder;
            orangeAreaMaterial = Tinted(area, OrangeArea);
            orangeFillMaterial = Tinted(fill, OrangeFill);
            redAreaMaterial = Tinted(area, RedArea);
            redFillMaterial = Tinted(fill, RedFill);
        }

        // The colour of a warning keeps the see-through amount of the material it comes from.
        public static Color Tint(Color source, Color tint)
        {
            return new Color(tint.r, tint.g, tint.b, source.a);
        }

        static Material Tinted(Material source, Color tint)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new Material(source);
            if (copy.HasProperty("_BaseColor"))
            {
                copy.SetColor("_BaseColor", Tint(copy.GetColor("_BaseColor"), tint));
            }
            else
            {
                copy.color = Tint(copy.color, tint);
            }

            return copy;
        }

        public int ActiveCount => active.Count;

        // The server sends every windup that is still going on in each snapshot. A client that joins late or gets
        // the first snapshot late draws only the time that is left, and a warning the server dropped disappears.
        public void BeginSync()
        {
            stamp++;
        }

        public void Show(int key, Vector3 origin, Vector2 worldDirection, AbilityShape shape, float range, float radius, float angle, float seconds, float remaining, bool avoidable = true)
        {
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].Key == key)
                {
                    active[i].Stamp = stamp;
                    return;
                }
            }

            var before = active.Count;
            Add(origin, worldDirection, shape, range, radius, angle, seconds, Mathf.Clamp(seconds - remaining, 0f, seconds), avoidable);
            if (active.Count > before)
            {
                active[active.Count - 1].Key = key;
                active[active.Count - 1].Stamp = stamp;
            }
        }

        public void EndSync()
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Key != 0 && active[i].Stamp != stamp)
                {
                    Retire(active[i]);
                    active.RemoveAt(i);
                }
            }
        }

        public void Add(Vector3 origin, Vector2 worldDirection, AbilityShape shape, float range, float radius, float angle, float seconds, float elapsed = 0f, bool avoidable = true)
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
            marker.Elapsed = elapsed;
            marker.Key = 0;

            var mesh = shape == AbilityShape.Circle ? cylinder : shape == AbilityShape.Cone ? Wedge(angle) : cube;
            marker.AreaRenderer.sharedMaterial = avoidable ? orangeAreaMaterial : redAreaMaterial;
            marker.FillRenderer.sharedMaterial = avoidable ? orangeFillMaterial : redFillMaterial;
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
            marker.Area = NewPart("TelegraphArea", out marker.AreaFilter, out marker.AreaRenderer);
            marker.Fill = NewPart("TelegraphFill", out marker.FillFilter, out marker.FillRenderer);
            return marker;
        }

        Transform NewPart(string name, out MeshFilter filter, out MeshRenderer renderer)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            filter = go.GetComponent<MeshFilter>();
            renderer = go.GetComponent<MeshRenderer>();
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
            public MeshRenderer AreaRenderer;
            public MeshRenderer FillRenderer;
            public AbilityShape Shape;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Range;
            public float Radius;
            public float Seconds;
            public float Elapsed;
            public int Key;
            public int Stamp;
        }
    }
}
