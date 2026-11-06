using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using EntityId = Game.Core.EntityId;

namespace Game.Client
{
    // A thin ring on the ground around the one idle enemy you are about to wake, showing how close you can get before it notices.
    // Only the nearest one is drawn, and only when you are within ShowBeyondRing of its edge, so a pack never fills the floor with
    // overlapping circles.
    public sealed class AggroRings
    {
        const float Height = 0.05f;
        const float Thickness = 0.09f;
        const float ShowBeyondRing = 2.5f;
        const int Segments = 64;
        const int MaxRings = 1;

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
            var entities = snapshot?.Entities;
            if (views.TryGetPosition(local, out var me) && entities != null)
            {
                var bestEdge = float.MaxValue;
                var best = default(EntityState);
                var bestPosition = default(Vector3);
                for (var i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (!ShouldShow(entity) || !views.TryGetPosition(entity.Id, out var position))
                    {
                        continue;
                    }

                    var edge = EdgeDistance(new Vector2(me.x, me.z), new Vector2(position.x, position.z), entity.AggroRange);
                    if (edge <= ShowBeyondRing && edge < bestEdge)
                    {
                        bestEdge = edge;
                        best = entity;
                        bestPosition = position;
                    }
                }

                if (bestEdge < float.MaxValue)
                {
                    Place(bestPosition, best.AggroQuarters);
                }
            }

            for (var i = used; i < pool.Count; i++)
            {
                if (pool[i].activeSelf)
                {
                    pool[i].SetActive(false);
                }
            }
        }

        // How far the player is from the ring's edge (negative once inside it).
        public static float EdgeDistance(Vector2 player, Vector2 enemy, float range)
        {
            return (enemy - player).magnitude - range;
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
