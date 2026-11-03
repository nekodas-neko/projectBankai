using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using EntityId = Game.Core.EntityId;

namespace Game.Client
{
    // A thin ring on the ground around every enemy that has not noticed you yet, showing how close you can get before it does.
    // Rings are pooled and only drawn for enemies near the local player, so a big map does not fill the floor with them.
    public sealed class AggroRings
    {
        const float Height = 0.05f;
        const float Thickness = 0.16f;
        const float ShowBeyondRing = 10f;
        const int Segments = 64;
        const int MaxRings = 24;

        readonly Transform parent;
        readonly Material material;
        readonly Dictionary<byte, Mesh> meshes = new Dictionary<byte, Mesh>();
        readonly List<GameObject> pool = new List<GameObject>();
        int used;

        public AggroRings(Transform parent, Material source)
        {
            this.parent = parent;
            material = Telegraphs.Tinted(source, new Color(1f, 0.82f, 0.25f));
        }

        public int Visible => used;

        // An idle enemy that can notice a player: alive, on the enemy side, with an aggro range and nobody targeted yet.
        public static bool ShouldShow(in EntityState entity)
        {
            return entity.Team == Team.Enemies && entity.Health > 0f && entity.AggroQuarters > 0 && !entity.Target.IsValid;
        }

        public void Sync(Snapshot snapshot, EntityId local, EntityViews views)
        {
            used = 0;
            var hasLocal = views.TryGetPosition(local, out var me);
            var entities = snapshot?.Entities;
            for (var i = 0; entities != null && i < entities.Count && used < MaxRings; i++)
            {
                var entity = entities[i];
                if (!ShouldShow(entity) || !views.TryGetPosition(entity.Id, out var position))
                {
                    continue;
                }

                if (hasLocal && new Vector2(position.x - me.x, position.z - me.z).magnitude > entity.AggroRange + ShowBeyondRing)
                {
                    continue;
                }

                Place(position, entity.AggroQuarters);
            }

            for (var i = used; i < pool.Count; i++)
            {
                if (pool[i].activeSelf)
                {
                    pool[i].SetActive(false);
                }
            }
        }

        void Place(Vector3 position, byte quarters)
        {
            if (used == pool.Count)
            {
                var ring = new GameObject("AggroRing", typeof(MeshFilter), typeof(MeshRenderer));
                ring.transform.SetParent(parent, false);
                ring.GetComponent<MeshRenderer>().sharedMaterial = material;
                pool.Add(ring);
            }

            var go = pool[used++];
            if (!meshes.TryGetValue(quarters, out var mesh))
            {
                mesh = BuildRing(quarters * 0.25f, Thickness);
                meshes[quarters] = mesh;
            }

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.position = new Vector3(position.x, Height, position.z);
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }
        }

        // A flat band in the ground plane, drawn on both faces so it shows whichever way the triangles wind.
        public static Mesh BuildRing(float radius, float thickness)
        {
            var inner = Mathf.Max(0.01f, radius - thickness);
            var vertices = new Vector3[Segments * 2];
            var normals = new Vector3[Segments * 2];
            var triangles = new int[Segments * 12];
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * radius;
                vertices[i * 2 + 1] = direction * inner;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;

                var next = (i + 1) % Segments;
                var o0 = i * 2;
                var i0 = i * 2 + 1;
                var o1 = next * 2;
                var i1 = next * 2 + 1;
                var t = i * 12;
                triangles[t] = o0; triangles[t + 1] = o1; triangles[t + 2] = i0;
                triangles[t + 3] = i0; triangles[t + 4] = o1; triangles[t + 5] = i1;
                triangles[t + 6] = o0; triangles[t + 7] = i0; triangles[t + 8] = o1;
                triangles[t + 9] = i0; triangles[t + 10] = i1; triangles[t + 11] = o1;
            }

            var mesh = new Mesh { name = "AggroRing", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
