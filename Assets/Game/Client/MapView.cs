using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Draws a dungeon's zones as floors, its walls, and a glowing barrier in every closed gate, from the same map the
    // simulation uses. When a boss falls and a gate opens, the barrier goes and the wall opens up.
    public sealed class MapView
    {
        // A stone dungeon: cold dark floors, a warmer worn path through the gates, near-black walls.
        public static readonly Color FloorColor = new Color(0.40f, 0.42f, 0.50f);
        public static readonly Color CorridorColor = new Color(0.52f, 0.45f, 0.38f);
        public static readonly Color WallColor = new Color(0.22f, 0.22f, 0.28f);
        public static readonly Color BarrierColor = new Color(1f, 0.8f, 0.25f);
        public static readonly Color WaterColor = new Color(0.20f, 0.52f, 0.95f);

        // Only the part of the dungeon near the player is drawn: a zone shows while the player is within ShowDistance of it,
        // and a wall or prop while it is within ShowDistance + WallExtra of the player.
        public const float ShowDistance = 14f;
        const float WallExtra = 8f;

        const float WallHeight = 1.3f;
        const float WallThickness = 0.6f;
        const float BarrierHeight = 1.6f;

        readonly WorldMap map;
        readonly Transform root;
        readonly Mesh cube;
        readonly Material wallMaterial;
        readonly List<GameObject> walls = new List<GameObject>();
        readonly List<GameObject> barriers = new List<GameObject>();
        readonly List<WorldMap.Wall> scratch = new List<WorldMap.Wall>();
        readonly List<Patch> patches = new List<Patch>();
        readonly List<GameObject> scenery = new List<GameObject>();
        readonly List<Patch> pools = new List<Patch>();
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
                patches.Add(new Patch(Box($"Floor {zone.Name}", zone.MinX, zone.MinY, zone.MaxX, zone.MaxY, -0.05f, 0.1f, floor), zone.MinX, zone.MinY, zone.MaxX, zone.MaxY));
            }

            for (var i = 0; i < map.Gates.Count; i++)
            {
                var gate = map.Gates[i];
                patches.Add(new Patch(Box($"Corridor {gate.Name}", gate.MinX, gate.MinY, gate.MaxX, gate.MaxY, -0.045f, 0.1f, corridor), gate.MinX, gate.MinY, gate.MaxX, gate.MaxY));
                map.GateSpan(i, out var minX, out var minY, out var maxX, out var maxY);
                var barrierBox = Box($"Barrier {gate.Name}", minX, minY, maxX, maxY, BarrierHeight * 0.5f, BarrierHeight, barrier);
                barriers.Add(barrierBox);
            }

            // Tagged patches (pools of water) lie on the floor; they fade with the area like a floor does.
            var water = Telegraphs.Tinted(transparent, WaterColor);
            for (var i = 0; i < map.Patches.Count; i++)
            {
                var pool = map.Patches[i];
                pools.Add(new Patch(Box($"Patch {pool.Name}", pool.MinX, pool.MinY, pool.MaxX, pool.MaxY, 0.02f, 0.05f, water), pool.MinX, pool.MinY, pool.MaxX, pool.MaxY));
            }

            Refresh();
        }

        public int PoolCount => pools.Count;

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

        public int VisibleFloors
        {
            get
            {
                var count = 0;
                foreach (var patch in patches)
                {
                    count += patch.Object.activeSelf ? 1 : 0;
                }

                return count;
            }
        }

        public int FloorCount => patches.Count;

        // Pieces that belong to a part of the map (pillars, rubble) and fade with it.
        public void AddScenery(GameObject piece)
        {
            scenery.Add(piece);
        }

        // Shows the floors, walls and props near the player and hides the rest, so only the current area is in view.
        // Cheap enough to call every frame: objects are only toggled when they change.
        public void Fade(float playerX, float playerY)
        {
            foreach (var patch in patches)
            {
                Toggle(patch.Object, patch.DistanceTo(playerX, playerY) <= ShowDistance);
            }

            foreach (var pool in pools)
            {
                Toggle(pool.Object, pool.DistanceTo(playerX, playerY) <= ShowDistance);
            }

            for (var i = 0; i < barriers.Count; i++)
            {
                var corridor = map.Zones.Count + i;
                var near = corridor < patches.Count && patches[corridor].DistanceTo(playerX, playerY) <= ShowDistance;
                Toggle(barriers[i], near && !map.IsOpen(i));
            }

            var wallRange = ShowDistance + WallExtra;
            for (var i = 0; i < scratch.Count && i < walls.Count; i++)
            {
                var centre = (scratch[i].A + scratch[i].B) * 0.5f;
                var near = Mathf.Max(0f, Mathf.Sqrt((centre.X - playerX) * (centre.X - playerX) + (centre.Y - playerY) * (centre.Y - playerY)) - scratch[i].Length * 0.5f) <= wallRange;
                Toggle(walls[i], near);
            }

            foreach (var piece in scenery)
            {
                if (piece == null)
                {
                    continue;
                }

                var position = piece.transform.position;
                var dx = position.x - playerX;
                var dz = position.z - playerY;
                Toggle(piece, dx * dx + dz * dz <= wallRange * wallRange);
            }
        }

        static void Toggle(GameObject target, bool on)
        {
            if (target.activeSelf != on)
            {
                target.SetActive(on);
            }
        }

        sealed class Patch
        {
            public readonly GameObject Object;
            readonly float minX;
            readonly float minY;
            readonly float maxX;
            readonly float maxY;

            public Patch(GameObject obj, float minX, float minY, float maxX, float maxY)
            {
                Object = obj;
                this.minX = minX;
                this.minY = minY;
                this.maxX = maxX;
                this.maxY = maxY;
            }

            public float DistanceTo(float x, float y)
            {
                var dx = Mathf.Max(0f, Mathf.Max(minX - x, x - maxX));
                var dy = Mathf.Max(0f, Mathf.Max(minY - y, y - maxY));
                return Mathf.Sqrt(dx * dx + dy * dy);
            }
        }

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
