using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class BossFrameTests
    {
        GameObject root;
        BossFrame frame;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BossFrameTest", typeof(RectTransform));
            frame = BossFrame.Build((RectTransform)root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ItStartsHidden()
        {
            Assert.IsFalse(frame.IsVisible);
        }

        [Test]
        public void ItShowsTheBossHealthAsAFraction()
        {
            frame.Show(300f, 1200f, false, false, 0);

            Assert.IsTrue(frame.IsVisible);
            Assert.AreEqual(0.25f, frame.FillFraction, 0.0001f);
        }

        [Test]
        public void ItSaysWhoTheBossIsFighting()
        {
            frame.Show(1f, 1f, true, true, 1);
            Assert.AreEqual("Boss - targeting YOU", frame.Text);

            frame.Show(1f, 1f, true, false, 2);
            Assert.AreEqual("Boss - targeting Player 2", frame.Text);

            frame.Show(1f, 1f, false, false, 0);
            Assert.AreEqual("Boss - idle", frame.Text);
        }

        [Test]
        public void ItHidesAgain()
        {
            frame.Show(1f, 1f, false, false, 0);

            frame.Hide();

            Assert.IsFalse(frame.IsVisible);
        }

        [Test]
        public void TheMarkerShowsUnderTheTargetAndHides()
        {
            var parent = new GameObject("MarkerParent").transform;
            try
            {
                var marker = new BossTargetMarker(parent, new Mesh(), null);
                Assert.IsFalse(marker.IsVisible);

                marker.Show(new Vector3(3f, 2f, -4f), 0f);
                Assert.IsTrue(marker.IsVisible);
                Assert.AreEqual(3f, marker.Position.x, 0.0001f);
                Assert.AreEqual(-4f, marker.Position.z, 0.0001f);
                Assert.Less(marker.Position.y, 0.5f);

                marker.Hide();
                Assert.IsFalse(marker.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }
    }
}
