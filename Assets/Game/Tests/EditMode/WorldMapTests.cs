using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WorldMapTests
    {
        // Two rooms with a doorway between them that opens when the first section is cleared.
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

        [Test]
        public void APointInsideAZoneIsWalkableAndOneOutsideIsNot()
        {
            var map = Map();

            Assert.IsTrue(map.Contains(new Vec2(0f, 0f)));
            Assert.IsFalse(map.Contains(new Vec2(0f, 13f)));
            Assert.IsFalse(map.Contains(new Vec2(50f, 0f)));
        }

        [Test]
        public void BodiesStayAWallMarginAwayFromTheEdge()
        {
            var map = Map();

            Assert.IsTrue(map.Contains(new Vec2(10f - WorldMap.Margin, 0f)));
            Assert.IsFalse(map.Contains(new Vec2(10f - WorldMap.Margin + 0.1f, 0f)));
            Assert.IsTrue(map.Contains(map.Constrain(new Vec2(50f, 0f))));
        }

        [Test]
        public void AClosedGateIsAWall()
        {
            var map = Map();

            Assert.IsFalse(map.IsOpen(0));
            Assert.IsFalse(map.Contains(new Vec2(0f, 13f)));
            Assert.AreEqual(10f - WorldMap.Margin, map.Constrain(new Vec2(0f, 13f)).Y, 0.001f);
        }

        [Test]
        public void ClearingTheSectionOpensTheGateAndTheWayThrough()
        {
            var map = Map();
            map.Progress = 1;

            Assert.IsTrue(map.IsOpen(0));
            Assert.IsTrue(map.Contains(new Vec2(0f, 13f)));

            var position = new Vec2(0f, 0f);
            var settings = SimulationSettings.Default.WithMap(map);
            for (var i = 0; i < 400; i++)
            {
                position = Movement.Step(position, new Vec2(0f, 1f), 6f, settings);
            }

            Assert.Greater(position.Y, 20f);
            Assert.IsTrue(map.Contains(position));
        }

        [Test]
        public void WalkingAtAWallStopsAtItAndWalkingAlongItSlides()
        {
            var map = Map();
            var settings = SimulationSettings.Default.WithMap(map);
            var position = new Vec2(8f, 0f);

            for (var i = 0; i < 100; i++)
            {
                position = Movement.Step(position, new Vec2(1f, 0f), 6f, settings);
            }

            Assert.AreEqual(10f - WorldMap.Margin, position.X, 0.001f);

            var before = position;
            position = Movement.Step(position, new Vec2(1f, 1f).Normalized, 6f, settings);

            Assert.AreEqual(10f - WorldMap.Margin, position.X, 0.001f);
            Assert.Greater(position.Y, before.Y);
        }

        [Test]
        public void ThePointOnTheOtherSideOfAWallIsNotTheNearestWalkableSpot()
        {
            var map = Map();

            var inside = map.Constrain(new Vec2(0f, 12.5f));

            Assert.IsTrue(map.Contains(inside));
            Assert.Less(inside.Y, 10f);
        }

        [Test]
        public void ThereIsNoGateWithoutProgressAndAnUnknownGateIsClosed()
        {
            var map = Map();

            Assert.IsFalse(map.IsOpen(-1));
            Assert.IsFalse(map.IsOpen(5));
        }

        [Test]
        public void AMapOfNothingIsNotMade()
        {
            Assert.IsNull(WorldMap.From(new DungeonDef { Id = "round" }));
            Assert.IsNull(WorldMap.From(null));
        }

        [Test]
        public void TheBoundsCoverEveryZone()
        {
            var map = Map();

            Assert.AreEqual(-10f, map.MinX);
            Assert.AreEqual(10f, map.MaxX);
            Assert.AreEqual(-10f, map.MinY);
            Assert.AreEqual(36f, map.MaxY);
        }

        [Test]
        public void WithoutAMapTheArenaIsStillRound()
        {
            var settings = SimulationSettings.Default;

            var moved = settings.Constrain(new Vec2(100f, 0f));

            Assert.AreEqual(settings.ArenaRadius, moved.Length, 0.001f);
        }

        [Test]
        public void ZoneAtFindsTheRoomAPointIsIn()
        {
            var map = Map();

            Assert.AreEqual(0, map.ZoneAt(new Vec2(0f, 0f)));
            Assert.AreEqual(1, map.ZoneAt(new Vec2(0f, 30f)));
            Assert.AreEqual(-1, map.ZoneAt(new Vec2(0f, 13f)));
        }
    }
}
