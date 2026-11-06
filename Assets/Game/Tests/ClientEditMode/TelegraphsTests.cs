using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class TelegraphsTests
    {
        GameObject root;
        Telegraphs telegraphs;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TelegraphsTest");
            telegraphs = new Telegraphs(root.transform, new Mesh(), new Mesh(), null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        void Sync(int key, float remaining)
        {
            telegraphs.Show(key, Vector3.zero, Vector2.up, AbilityShape.Circle, 4f, 4f, 0f, 1f, remaining);
        }

        [Test]
        public void AWarningTintKeepsTheSeeThroughAmountOfTheMaterial()
        {
            var tinted = Telegraphs.Tint(new Color(0.1f, 0.2f, 0.3f, 0.4f), Telegraphs.RedArea);

            Assert.AreEqual(Telegraphs.RedArea.r, tinted.r);
            Assert.AreEqual(Telegraphs.RedArea.g, tinted.g);
            Assert.AreEqual(Telegraphs.RedArea.b, tinted.b);
            Assert.AreEqual(0.4f, tinted.a);
        }

        [Test]
        public void RedAndOrangeWarningsAreClearlyDifferentColours()
        {
            Assert.Greater(Telegraphs.OrangeArea.g, Telegraphs.RedArea.g + 0.3f);
            Assert.Greater(Telegraphs.OrangeFill.g, Telegraphs.RedFill.g + 0.3f);
        }

        [Test]
        public void ARedWarningAndAnOrangeWarningBothShow()
        {
            telegraphs.BeginSync();
            telegraphs.Show(1, Vector3.zero, Vector2.up, AbilityShape.Circle, 4f, 4f, 0f, 1f, 1f, true);
            telegraphs.Show(2, Vector3.zero, Vector2.up, AbilityShape.Circle, 4f, 4f, 0f, 1f, 1f, false);
            telegraphs.EndSync();

            Assert.AreEqual(2, telegraphs.ActiveCount);
        }

        [Test]
        public void AWindupFromTheServerShowsOneWarning()
        {
            telegraphs.BeginSync();
            Sync(5, 1f);
            telegraphs.EndSync();

            Assert.AreEqual(1, telegraphs.ActiveCount);
        }

        [Test]
        public void TheSameWindupInTheNextSnapshotDoesNotRestartIt()
        {
            telegraphs.BeginSync();
            Sync(5, 1f);
            telegraphs.EndSync();
            telegraphs.Tick(0.5f);

            telegraphs.BeginSync();
            Sync(5, 0.5f);
            telegraphs.EndSync();
            telegraphs.Tick(0.4f);

            Assert.AreEqual(1, telegraphs.ActiveCount);
            telegraphs.Tick(0.2f);
            Assert.AreEqual(0, telegraphs.ActiveCount);
        }

        [Test]
        public void ALateWindupOnlyShowsTheTimeThatIsLeft()
        {
            telegraphs.BeginSync();
            Sync(5, 0.2f);
            telegraphs.EndSync();

            telegraphs.Tick(0.3f);

            Assert.AreEqual(0, telegraphs.ActiveCount);
        }

        [Test]
        public void AWarningTheServerNoLongerSendsDisappears()
        {
            telegraphs.BeginSync();
            Sync(5, 1f);
            telegraphs.EndSync();

            telegraphs.BeginSync();
            telegraphs.EndSync();

            Assert.AreEqual(0, telegraphs.ActiveCount);
        }

        [Test]
        public void AnAlreadyFinishedWindupShowsNothing()
        {
            telegraphs.BeginSync();
            Sync(5, 0f);
            telegraphs.EndSync();
            telegraphs.Tick(0.01f);

            Assert.AreEqual(0, telegraphs.ActiveCount);
        }
    }
}
