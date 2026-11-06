using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WorldMapWallTests
    {
        static WorldMap Map()
        {
            return new WorldMap(
                new[]
                {
                    new ZoneDef { Name = "A", MinX = -10f, MinY = -10f, MaxX = 10f, MaxY = 10f },
                    new ZoneDef { Name = "B", MinX = -10f, MinY = 16f, MaxX = 10f, MaxY = 36f },
                },
                new[] { new GateDef { Name = "door", MinX = -2f, MinY = 9f, MaxX = 2f, MaxY = 17f, OpenAfter = 0 } });
        }

        static float Total(List<WorldMap.Wall> walls)
        {
            var total = 0f;
            foreach (var wall in walls)
            {
                total += wall.Length;
            }

            return total;
        }

        [Test]
        public void WithTheGateClosedEveryZoneIsWalledAllRoundAndTheGateHasNoWalls()
        {
            var walls = new List<WorldMap.Wall>();

            Map().Walls(walls);

            Assert.AreEqual(8, walls.Count);
            Assert.AreEqual(2f * (20f + 20f) + 2f * (20f + 20f), Total(walls), 0.01f);
        }

        [Test]
        public void OpeningTheGateLeavesAGapInBothZonesAndAddsTheCorridorWalls()
        {
            var map = Map();
            map.Progress = 1;
            var walls = new List<WorldMap.Wall>();

            map.Walls(walls);

            // The two zones lose the doorway (4 units each) and the corridor gains a wall on each side, 6 units each (from y = 10 to y = 16).
            Assert.AreEqual(2f * 80f - 2f * 4f + 2f * 6f, Total(walls), 0.01f);
            var corridor = walls.FindAll(w => !w.Horizontal && System.Math.Abs(System.Math.Abs(w.A.X) - 2f) < 0.01f);
            Assert.AreEqual(2, corridor.Count);
            foreach (var wall in corridor)
            {
                Assert.AreEqual(6f, wall.Length, 0.01f);
            }
        }

        [Test]
        public void NoWallStandsInTheDoorway()
        {
            var map = Map();
            map.Progress = 1;
            var walls = new List<WorldMap.Wall>();

            map.Walls(walls);

            foreach (var wall in walls)
            {
                if (wall.Horizontal && (System.Math.Abs(wall.A.Y - 10f) < 0.01f || System.Math.Abs(wall.A.Y - 16f) < 0.01f))
                {
                    var low = System.Math.Min(wall.A.X, wall.B.X);
                    var high = System.Math.Max(wall.A.X, wall.B.X);
                    Assert.IsTrue(high <= -2f + 0.01f || low >= 2f - 0.01f, $"wall from {low} to {high} blocks the doorway");
                }
            }
        }

        [Test]
        public void AClosedGateBlocksOnlyTheSpanBetweenTheZones()
        {
            var map = Map();

            map.GateSpan(0, out var minX, out var minY, out var maxX, out var maxY);

            Assert.AreEqual(-2f, minX);
            Assert.AreEqual(2f, maxX);
            Assert.AreEqual(10f, minY);
            Assert.AreEqual(16f, maxY);
        }

        [Test]
        public void ASideWaysGateIsSpannedAlongItsLongAxisToo()
        {
            var map = new WorldMap(
                new[]
                {
                    new ZoneDef { Name = "left", MinX = -20f, MinY = -5f, MaxX = -2f, MaxY = 5f },
                    new ZoneDef { Name = "right", MinX = 4f, MinY = -5f, MaxX = 20f, MaxY = 5f },
                },
                new[] { new GateDef { Name = "door", MinX = -3f, MinY = -1f, MaxX = 5f, MaxY = 1f, OpenAfter = 0 } });

            map.GateSpan(0, out var minX, out _, out var maxX, out _);

            Assert.AreEqual(-2f, minX);
            Assert.AreEqual(4f, maxX);
        }

        [Test]
        public void AMapWithOneZoneIsJustABox()
        {
            var map = new WorldMap(new[] { new ZoneDef { Name = "box", MinX = 0f, MinY = 0f, MaxX = 10f, MaxY = 6f } }, null);
            var walls = new List<WorldMap.Wall>();

            map.Walls(walls);

            Assert.AreEqual(4, walls.Count);
            Assert.AreEqual(32f, Total(walls), 0.01f);
        }
    }
}
