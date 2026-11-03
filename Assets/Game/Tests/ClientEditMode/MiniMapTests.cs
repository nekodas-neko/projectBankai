using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Game.Core.EntityId;

namespace Game.Client.Tests
{
    public class MiniMapTests
    {
        GameObject root;
        MiniMap miniMap;

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

        static EntityState Entity(int id, float x, float y, int owner = 0, float health = 10f, byte effects = 0)
        {
            return new EntityState { Id = new EntityId(id), Owner = new PlayerId(owner), Position = new Vec2(x, y), Health = health, MaxHealth = 10f, Effects = effects };
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MiniMapTest", typeof(RectTransform));
            miniMap = MiniMap.Build((RectTransform)root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ItIsHiddenUntilItHasAMap()
        {
            Assert.IsFalse(miniMap.IsVisible);

            miniMap.Setup(Map());
            Assert.IsTrue(miniMap.IsVisible);

            miniMap.Setup(null);
            Assert.IsFalse(miniMap.IsVisible);
        }

        [Test]
        public void TheWholeMapFitsInsideTheBox()
        {
            miniMap.Setup(Map());

            var low = miniMap.PointFor(new Vec2(-10f, -10f));
            var high = miniMap.PointFor(new Vec2(10f, 36f));

            Assert.GreaterOrEqual(low.x, 0f);
            Assert.GreaterOrEqual(low.y, 0f);
            Assert.LessOrEqual(high.x, 130f);
            Assert.LessOrEqual(high.y, 300f);
            Assert.Greater(high.y, low.y);
            Assert.Greater(high.x, low.x);
        }

        [Test]
        public void NorthIsUpAndWestIsLeft()
        {
            miniMap.Setup(Map());

            Assert.Greater(miniMap.PointFor(new Vec2(0f, 30f)).y, miniMap.PointFor(new Vec2(0f, 0f)).y);
            Assert.Greater(miniMap.PointFor(new Vec2(5f, 0f)).x, miniMap.PointFor(new Vec2(-5f, 0f)).x);
        }

        [Test]
        public void ItDrawsADotForEveryoneAliveAndNotForTheDead()
        {
            miniMap.Setup(Map());
            var snapshot = new Snapshot();
            snapshot.Entities.Add(Entity(1, 0f, 0f, owner: 1));
            snapshot.Entities.Add(Entity(2, 3f, 2f, owner: 2));
            snapshot.Entities.Add(Entity(3, 0f, 6f));
            snapshot.Entities.Add(Entity(4, 0f, 31f, effects: EffectFlags.Boss));
            snapshot.Entities.Add(Entity(5, 1f, 1f, health: 0f));

            miniMap.Refresh(snapshot, new EntityId(1));

            Assert.AreEqual(4, miniMap.VisibleDots);
        }

        [Test]
        public void DotsGoWhenSomeoneDiesAndTheMapDoesNotGrowForever()
        {
            miniMap.Setup(Map());
            var snapshot = new Snapshot();
            for (var i = 1; i <= 10; i++)
            {
                snapshot.Entities.Add(Entity(i, i, i));
            }

            miniMap.Refresh(snapshot, new EntityId(1));
            Assert.AreEqual(10, miniMap.VisibleDots);

            var fewer = new Snapshot();
            fewer.Entities.Add(Entity(1, 0f, 0f));
            miniMap.Refresh(fewer, new EntityId(1));

            Assert.AreEqual(1, miniMap.VisibleDots);
        }

        [Test]
        public void TheGateIsGoldWhileClosedAndDullWhenOpen()
        {
            var map = Map();
            miniMap.Setup(map);
            var snapshot = new Snapshot();

            miniMap.Refresh(snapshot, new EntityId(1));
            var closed = GateColor();
            map.Progress = 1;
            miniMap.Refresh(snapshot, new EntityId(1));
            var open = GateColor();

            Assert.AreEqual(MiniMap.ClosedGateColor, closed);
            Assert.AreEqual(MiniMap.OpenGateColor, open);
        }

        [Test]
        public void RefreshingWithoutAMapDoesNothing()
        {
            miniMap.Refresh(new Snapshot(), new EntityId(1));

            Assert.IsFalse(miniMap.IsVisible);
            Assert.AreEqual(0, miniMap.VisibleDots);
        }

        Color GateColor()
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith("Gate"))
                {
                    return image.color;
                }
            }

            Assert.Fail("no gate on the mini map");
            return default;
        }
    }
}
