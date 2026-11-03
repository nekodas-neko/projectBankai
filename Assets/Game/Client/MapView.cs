using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Draws a dungeon's zones as floors, its walls, and a glowing barrier in every closed gate, from the same map the
    // simulation uses. When a boss falls and a gate opens, the barrier goes and the wall opens up.
    public sealed class MapView
    {
        public static readonly Color FloorColor = new Color(0.30f, 0.44f, 0.24f);
        public static readonly Color CorridorColor = new Color(0.45f, 0.38f, 0.27f);
        public static readonly Color WallColor = new Color(0.38f, 0.35f, 0.33f);
        public static readonly Color BarrierColor = new Color(1f, 0.8f, 0.25f);

        const float WallHeight = 2.4f;
        const float WallThickness = 0.6f;
        const float BarrierHeight = 2.6f;

        readonly WorldMap map;
        readonly Transform root;
        readonly Mesh cube;
        readonly Material wallMaterial;
        readonly List<GameObject> walls = new List<GameObject>();
        readonly List<GameObject> barriers = new List<GameObject>();
        readonly List<WorldMap.Wall> scratch = new List<WorldMap.Wall>();
        int shownProgress = -1;

        public MapView(Transform parent, WorldMap map, Mesh cube, Material ground, Material transparent)
        {
            this.map = map;
            this.cube = cube;
            root = new GameObject("DungeonMap").transform;
            root.SetParent(parent, false);

            var floor = Telegraphs.Tinted(ground, FloorColor);
            var corridor = Telegraphs.Tinted(ground, CorridorColor);
            wallMaterial = Telegraphs.Tinted(ground, WallColor);
            var barrier = Telegraphs.Tinted(transparent, BarrierColor);

            for (var i = 0; i < map.Zones.Count; i++)
            {
                var zone = map.Zones[i];
                Box($"Floor {zone.Name}", zone.MinX, zone.MinY, zone.MaxX, zone.MaxY, -0.05f, 0.1f, floor);
            }

            for (var i = 0; i < map.Gates.Count; i++)
            {
                var gate = map.Gates[i];
                Box($"Corridor {gate.Name}", gate.MinX, gate.MinY, gate.MaxX, gate.MaxY, -0.045f, 0.1f, corridor);
                map.GateSpan(i, out var minX, out var minY, out var maxX, out var maxY);
                barriers.Add(Box($"Barrier {gate.Name}", minX, minY, maxX, maxY, BarrierHeight * 0.5f, BarrierHeight, barrier));
            }

            Refresh();
        }

        public int WallCount { get; private set; }

        public int ClosedGateCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < barriers.Count; i++)
                {
                    count += barriers[i].activeSelf ? 1 : 0;
                }

                return count;
            }
        }

        public Transform Root => root;

        public bool BarrierVisible(int gate)
        {
            return gate >= 0 && gate < barriers.Count && barriers[gate].activeSelf;
        }

        // Cheap to call every frame: it only does work when a gate has opened or closed.
        public void Refresh()
        {
            if (map.Progress == shownProgress)
            {
                return;
            }

            shownProgress = map.Progress;
            for (var i = 0; i < barriers.Count; i++)
            {
                barriers[i].SetActive(!map.IsOpen(i));
            }

            map.Walls(scratch);
            for (var i = 0; i < scratch.Count; i++)
            {
                if (i >= walls.Count)
                {
                    walls.Add(NewCube("Wall", wallMaterial));
                }

                Place(walls[i], scratch[i]);
                walls[i].SetActive(true);
            }

            for (var i = scratch.Count; i < walls.Count; i++)
            {
                walls[i].SetActive(false);
            }

            WallCount = scratch.Count;
        }

        void Place(GameObject wall, WorldMap.Wall segment)
        {
            var centre = (segment.A + segment.B) * 0.5f;
            var along = segment.Length + WallThickness;
            wall.transform.position = new Vector3(centre.X, WallHeight * 0.5f, centre.Y);
            wall.transform.localScale = segment.Horizontal ? new Vector3(along, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, along);
        }

        GameObject Box(string name, float minX, float minY, float maxX, float maxY, float y, float height, Material material)
        {
            var box = NewCube(name, material);
            box.transform.position = new Vector3((minX + maxX) * 0.5f, y, (minY + maxY) * 0.5f);
            box.transform.localScale = new Vector3(maxX - minX, height, maxY - minY);
            return box;
        }

        GameObject NewCube(string name, Material material)
        {
            var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(root, false);
            box.GetComponent<MeshFilter>().sharedMesh = cube;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }
    }
}
