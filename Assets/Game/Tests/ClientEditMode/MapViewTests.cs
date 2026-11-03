using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class MapViewTests
    {
        GameObject root;

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

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MapViewTest", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ItBuildsAFloorForEveryZoneAndABarrierForEveryGate()
        {
            var view = new MapView(root.transform, Map(), new Mesh(), null, null);

            var floors = 0;
            foreach (Transform child in view.Root)
            {
                floors += child.name.StartsWith("Floor") ? 1 : 0;
            }

            Assert.AreEqual(2, floors);
            Assert.AreEqual(1, view.ClosedGateCount);
            Assert.IsTrue(view.BarrierVisible(0));
        }

        [Test]
        public void WithTheGateClosedEveryZoneIsWalledOff()
        {
            var view = new MapView(root.transform, Map(), new Mesh(), null, null);

            Assert.AreEqual(8, view.WallCount);
        }

        [Test]
        public void OpeningTheGateRemovesTheBarrierAndOpensTheWall()
        {
            var map = Map();
            var view = new MapView(root.transform, map, new Mesh(), null, null);

            map.Progress = 1;
            view.Refresh();

            Assert.IsFalse(view.BarrierVisible(0));
            Assert.AreEqual(0, view.ClosedGateCount);
            Assert.AreEqual(12, view.WallCount);
        }

        [Test]
        public void ClosingTheGateAgainRestoresTheBarrierAndTheWall()
        {
            var map = Map();
            var view = new MapView(root.transform, map, new Mesh(), null, null);
            map.Progress = 1;
            view.Refresh();

            map.Progress = 0;
            view.Refresh();

            Assert.IsTrue(view.BarrierVisible(0));
            Assert.AreEqual(8, view.WallCount);
        }

        [Test]
        public void WallsAreStandingBoxesAlongTheEdgesOfTheZones()
        {
            var view = new MapView(root.transform, Map(), new Mesh(), null, null);

            var found = false;
            foreach (Transform child in view.Root)
            {
                if (child.name == "Wall" && child.gameObject.activeSelf && Mathf.Abs(child.position.z - 10f) < 0.01f)
                {
                    found = true;
                    Assert.Greater(child.localScale.y, 1.5f);
                    Assert.AreEqual(20f + 0.6f, child.localScale.x, 0.01f);
                }
            }

            Assert.IsTrue(found);
        }

        [Test]
        public void TheBarrierFillsOnlyTheSpanBetweenTheZones()
        {
            var view = new MapView(root.transform, Map(), new Mesh(), null, null);

            Transform barrier = null;
            foreach (Transform child in view.Root)
            {
                barrier = child.name.StartsWith("Barrier") ? child : barrier;
            }

            Assert.IsNotNull(barrier);
            Assert.AreEqual(13f, barrier.position.z, 0.01f);
            Assert.AreEqual(6f, barrier.localScale.z, 0.01f);
            Assert.AreEqual(4f, barrier.localScale.x, 0.01f);
        }

        [Test]
        public void RefreshingWithNothingNewDoesNotRebuild()
        {
            var view = new MapView(root.transform, Map(), new Mesh(), null, null);
            var before = view.Root.childCount;

            view.Refresh();
            view.Refresh();

            Assert.AreEqual(before, view.Root.childCount);
            Assert.AreEqual(8, view.WallCount);
        }
    }
}
