using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // The shipped cave is a winding map with side caves, and it has to actually be walkable: every pack and boss reachable
    // once the gates before it are open, and nothing reachable around a closed gate.
    public class CaveLayoutTests
    {
        const float Cell = 0.5f;
        const float Body = WorldMap.Margin;

        static DungeonDef Cave()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);
            return dungeons.Get("slime_cave");
        }

        static bool Walkable(WorldMap map, Vec2 p)
        {
            return map.Contains(p)
                && map.Contains(new Vec2(p.X + Body, p.Y)) && map.Contains(new Vec2(p.X - Body, p.Y))
                && map.Contains(new Vec2(p.X, p.Y + Body)) && map.Contains(new Vec2(p.X, p.Y - Body));
        }

        static HashSet<(int, int)> Reachable(WorldMap map)
        {
            var seen = new HashSet<(int, int)>();
            var queue = new Queue<(int, int)>();
            var start = (0, 0);
            Assert.IsTrue(Walkable(map, new Vec2(0f, 0f)), "the start must be walkable");
            seen.Add(start);
            queue.Enqueue(start);
            var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in steps)
                {
                    var next = (x + dx, y + dy);
                    if (seen.Contains(next) || !Walkable(map, new Vec2(next.Item1 * Cell, next.Item2 * Cell)))
                    {
                        continue;
                    }

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return seen;
        }

        static bool CanReach(HashSet<(int, int)> reach, float x, float y)
        {
            return reach.Contains(((int)System.Math.Round(x / Cell), (int)System.Math.Round(y / Cell)));
        }

        [Test]
        public void EveryPackAndBossIsReachableOnceTheGatesBeforeItAreOpen()
        {
            var cave = Cave();
            for (var section = 0; section < cave.Rooms.Length; section++)
            {
                var map = WorldMap.From(cave);
                map.Progress = section;
                var reach = Reachable(map);
                foreach (var spawn in cave.Rooms[section].Spawns)
                {
                    var x = spawn.X + cave.Rooms[section].OffsetX;
                    var y = spawn.Y + cave.Rooms[section].OffsetY;
                    Assert.IsTrue(CanReach(reach, x, y), $"section {section + 1}: a {spawn.Class} at ({x}, {y}) cannot be walked to");
                }
            }
        }

        [Test]
        public void TheNextSectionIsWalledOffUntilTheBossIsDown()
        {
            var cave = Cave();
            for (var section = 0; section < cave.Rooms.Length - 1; section++)
            {
                var map = WorldMap.From(cave);
                map.Progress = section;
                var reach = Reachable(map);
                var next = cave.Rooms[section + 1];
                foreach (var spawn in next.Spawns)
                {
                    Assert.IsFalse(CanReach(reach, spawn.X + next.OffsetX, spawn.Y + next.OffsetY), $"a {spawn.Class} of section {section + 2} can be reached around the gate");
                }
            }
        }

        [Test]
        public void TheCaveIsNotAStraightLine()
        {
            var cave = Cave();
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var room in cave.Rooms)
            {
                foreach (var spawn in room.Spawns)
                {
                    if (spawn.Boss)
                    {
                        min = System.Math.Min(min, spawn.X + room.OffsetX);
                        max = System.Math.Max(max, spawn.X + room.OffsetX);
                    }
                }
            }

            Assert.Greater(max - min, 30f, "the bosses should not sit in one column");
            Assert.GreaterOrEqual(cave.Zones.Length, 10, "side caves and passages");
        }
    }
}
