using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ZoneDef
    {
        public string Name { get; set; }
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }
    }

    // A tagged patch of ground such as a pool of water or a stretch of cover. It does not block anything; an ability can
    // look at whether a body stands on a patch with a given tag (see AbilityDef.ShelterTag).
    public sealed class PatchDef
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }
    }

    // A doorway between two zones. It is a wall until the section named by OpenAfter has been cleared (its boss is dead).
    // A gate is a rectangle that overlaps a little into the zones on both ends, so walking through is continuous.
    public sealed class GateDef
    {
        public string Name { get; set; }
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }
        public int OpenAfter { get; set; }
    }

    // Where anything can stand: the zones, plus the gates that are open. The server and every client use the same rule,
    // so a client predicting its own movement agrees with the server about the walls.
    public sealed class WorldMap
    {
        // How far a body stays from a wall.
        public const float Margin = 0.45f;

        // Constrain puts a point exactly on the margin; rounding must not make that spot count as a wall.
        const float Tolerance = 0.001f;

        readonly ZoneDef[] zones;
        readonly GateDef[] gates;
        readonly PatchDef[] patches;

        public WorldMap(ZoneDef[] zones, GateDef[] gates, PatchDef[] patches = null)
        {
            this.patches = patches ?? Array.Empty<PatchDef>();
            this.zones = zones ?? Array.Empty<ZoneDef>();
            this.gates = gates ?? Array.Empty<GateDef>();
            var first = this.zones.Length > 0;
            MinX = first ? float.MaxValue : 0f;
            MinY = first ? float.MaxValue : 0f;
            MaxX = first ? float.MinValue : 0f;
            MaxY = first ? float.MinValue : 0f;
            foreach (var zone in this.zones)
            {
                MinX = Math.Min(MinX, zone.MinX);
                MinY = Math.Min(MinY, zone.MinY);
                MaxX = Math.Max(MaxX, zone.MaxX);
                MaxY = Math.Max(MaxY, zone.MaxY);
            }
        }

        // How many sections have been cleared. Set by the server, and by a client that has its own copy of the map.
        public int Progress { get; set; }

        // The server's map is the truth; a client that shares the object does not write to it.
        public bool Authoritative { get; set; }

        public IReadOnlyList<ZoneDef> Zones => zones;

        public IReadOnlyList<GateDef> Gates => gates;

        public IReadOnlyList<PatchDef> Patches => patches;

        public float MinX { get; }

        public float MinY { get; }

        public float MaxX { get; }

        public float MaxY { get; }

        public static WorldMap From(DungeonDef dungeon)
        {
            return dungeon != null && dungeon.Zones.Length > 0 ? new WorldMap(dungeon.Zones, dungeon.Gates, dungeon.Patches) : null;
        }

        public bool IsOpen(int gateIndex)
        {
            return gateIndex >= 0 && gateIndex < gates.Length && Progress > gates[gateIndex].OpenAfter;
        }

        // True when the point is on a patch with this tag.
        public bool OnPatch(Vec2 point, string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            for (var i = 0; i < patches.Length; i++)
            {
                if (patches[i].Tag == tag && Inside(point, patches[i].MinX, patches[i].MinY, patches[i].MaxX, patches[i].MaxY))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(Vec2 point)
        {
            for (var i = 0; i < zones.Length; i++)
            {
                if (Inside(point, zones[i].MinX, zones[i].MinY, zones[i].MaxX, zones[i].MaxY))
                {
                    return true;
                }
            }

            for (var i = 0; i < gates.Length; i++)
            {
                if (IsOpen(i) && Inside(point, gates[i].MinX, gates[i].MinY, gates[i].MaxX, gates[i].MaxY))
                {
                    return true;
                }
            }

            return false;
        }

        // The nearest spot a body can stand. Moving into a wall lands on the wall, so movement slides along it.
        public Vec2 Constrain(Vec2 point)
        {
            if (Contains(point))
            {
                return point;
            }

            var best = point;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < zones.Length; i++)
            {
                Consider(point, zones[i].MinX, zones[i].MinY, zones[i].MaxX, zones[i].MaxY, ref best, ref bestSqr);
            }

            for (var i = 0; i < gates.Length; i++)
            {
                if (IsOpen(i))
                {
                    Consider(point, gates[i].MinX, gates[i].MinY, gates[i].MaxX, gates[i].MaxY, ref best, ref bestSqr);
                }
            }

            return best;
        }

        // A straight wall along an edge of a walkable rectangle. Horizontal walls have the same Y at both ends.
        public readonly struct Wall
        {
            public readonly Vec2 A;
            public readonly Vec2 B;

            public Wall(Vec2 a, Vec2 b)
            {
                A = a;
                B = b;
            }

            public bool Horizontal => Math.Abs(A.Y - B.Y) < 1e-4f;

            public float Length => (B - A).Length;
        }

        // The edges of everything that can be stood on right now, except where another walkable area continues on the other side:
        // a closed gate leaves its zones walled off, an open one leaves a gap and gets its own side walls.
        public void Walls(List<Wall> into)
        {
            into.Clear();
            for (var i = 0; i < zones.Length; i++)
            {
                AddWalls(zones[i].MinX, zones[i].MinY, zones[i].MaxX, zones[i].MaxY, i, into);
            }

            for (var i = 0; i < gates.Length; i++)
            {
                if (IsOpen(i))
                {
                    AddWalls(gates[i].MinX, gates[i].MinY, gates[i].MaxX, gates[i].MaxY, zones.Length + i, into);
                }
            }
        }

        // The part of a gate between the zones it joins: what a closed gate blocks.
        public void GateSpan(int gateIndex, out float minX, out float minY, out float maxX, out float maxY)
        {
            var gate = gates[gateIndex];
            minX = gate.MinX;
            minY = gate.MinY;
            maxX = gate.MaxX;
            maxY = gate.MaxY;
            var tall = gate.MaxY - gate.MinY >= gate.MaxX - gate.MinX;
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (!(gate.MinX < zone.MaxX && gate.MaxX > zone.MinX && gate.MinY < zone.MaxY && gate.MaxY > zone.MinY))
                {
                    continue;
                }

                var centre = tall ? (zone.MinY + zone.MaxY) * 0.5f : (zone.MinX + zone.MaxX) * 0.5f;
                var gateCentre = tall ? (gate.MinY + gate.MaxY) * 0.5f : (gate.MinX + gate.MaxX) * 0.5f;
                if (tall)
                {
                    if (centre < gateCentre)
                    {
                        minY = Math.Max(minY, zone.MaxY);
                    }
                    else
                    {
                        maxY = Math.Min(maxY, zone.MinY);
                    }
                }
                else if (centre < gateCentre)
                {
                    minX = Math.Max(minX, zone.MaxX);
                }
                else
                {
                    maxX = Math.Min(maxX, zone.MinX);
                }
            }
        }

        void AddWalls(float minX, float minY, float maxX, float maxY, int self, List<Wall> into)
        {
            AddEdge(minY, minX, maxX, true, self, into);
            AddEdge(maxY, minX, maxX, true, self, into);
            AddEdge(minX, minY, maxY, false, self, into);
            AddEdge(maxX, minY, maxY, false, self, into);
        }

        // One edge: "line" is the fixed coordinate (Y for a horizontal edge), from..to the span along it. Spans that another
        // walkable rectangle passes straight through are not walls.
        void AddEdge(float line, float from, float to, bool horizontal, int self, List<Wall> into)
        {
            var covered = new List<(float, float)>();
            for (var i = 0; i < zones.Length + gates.Length; i++)
            {
                if (i == self || (i >= zones.Length && !IsOpen(i - zones.Length)))
                {
                    continue;
                }

                float minX, minY, maxX, maxY;
                if (i < zones.Length)
                {
                    minX = zones[i].MinX;
                    minY = zones[i].MinY;
                    maxX = zones[i].MaxX;
                    maxY = zones[i].MaxY;
                }
                else
                {
                    var gate = gates[i - zones.Length];
                    minX = gate.MinX;
                    minY = gate.MinY;
                    maxX = gate.MaxX;
                    maxY = gate.MaxY;
                }

                var crosses = horizontal ? minY < line && line < maxY : minX < line && line < maxX;
                if (!crosses)
                {
                    continue;
                }

                var low = Math.Max(from, horizontal ? minX : minY);
                var high = Math.Min(to, horizontal ? maxX : maxY);
                if (high > low)
                {
                    covered.Add((low, high));
                }
            }

            covered.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            var cursor = from;
            for (var i = 0; i < covered.Count; i++)
            {
                if (covered[i].Item1 > cursor + 1e-4f)
                {
                    into.Add(horizontal ? new Wall(new Vec2(cursor, line), new Vec2(covered[i].Item1, line)) : new Wall(new Vec2(line, cursor), new Vec2(line, covered[i].Item1)));
                }

                cursor = Math.Max(cursor, covered[i].Item2);
            }

            if (to > cursor + 1e-4f)
            {
                into.Add(horizontal ? new Wall(new Vec2(cursor, line), new Vec2(to, line)) : new Wall(new Vec2(line, cursor), new Vec2(line, to)));
            }
        }

        // The zone a point is in, or -1.
        public int ZoneAt(Vec2 point)
        {
            for (var i = 0; i < zones.Length; i++)
            {
                if (point.X >= zones[i].MinX && point.X <= zones[i].MaxX && point.Y >= zones[i].MinY && point.Y <= zones[i].MaxY)
                {
                    return i;
                }
            }

            return -1;
        }

        static bool Inside(Vec2 point, float minX, float minY, float maxX, float maxY)
        {
            return point.X >= minX + Margin - Tolerance && point.X <= maxX - Margin + Tolerance && point.Y >= minY + Margin - Tolerance && point.Y <= maxY - Margin + Tolerance;
        }

        static void Consider(Vec2 point, float minX, float minY, float maxX, float maxY, ref Vec2 best, ref float bestSqr)
        {
            var x = Math.Max(minX + Margin, Math.Min(maxX - Margin, point.X));
            var y = Math.Max(minY + Margin, Math.Min(maxY - Margin, point.Y));
            var candidate = new Vec2(x, y);
            var sqr = (candidate - point).SqrLength;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }
    }
}
