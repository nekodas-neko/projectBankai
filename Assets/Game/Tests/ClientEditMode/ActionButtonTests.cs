using NUnit.Framework;

namespace Game.Client.Tests
{
    public class ActionButtonTests
    {
        [Test]
        public void AnAffordableAbilityShowsOnlyItsCooldown()
        {
            Assert.AreEqual(0f, ActionButton.RingFill(0f, 100f, 100f, 30f));
            Assert.AreEqual(0.4f, ActionButton.RingFill(0.4f, 100f, 100f, 30f), 0.0001f);
        }

        [Test]
        public void AnAbilityTheResourceCannotPayForShowsHowFarOffItIs()
        {
            Assert.AreEqual(0.5f, ActionButton.RingFill(0f, 15f, 100f, 30f), 0.0001f);
            Assert.AreEqual(1f, ActionButton.RingFill(0f, 0f, 100f, 30f), 0.0001f);
        }

        [Test]
        public void TheRingShowsWhicheverIsFurtherOff()
        {
            Assert.AreEqual(0.8f, ActionButton.RingFill(0.8f, 15f, 100f, 30f), 0.0001f);
            Assert.AreEqual(0.5f, ActionButton.RingFill(0.2f, 15f, 100f, 30f), 0.0001f);
        }

        [Test]
        public void AnAttackThatGivesResourceNeverDarkensTheRing()
        {
            Assert.AreEqual(0f, ActionButton.RingFill(0f, 0f, 100f, -10f));
            Assert.AreEqual(0f, ActionButton.RingFill(0f, 0f, 100f, 0f));
        }

        [Test]
        public void WithoutAResourceBarTheCostIsIgnored()
        {
            Assert.AreEqual(0.3f, ActionButton.RingFill(0.3f, 0f, 0f, 30f), 0.0001f);
        }

        [Test]
        public void TheFillStaysInsideZeroAndOne()
        {
            Assert.AreEqual(1f, ActionButton.RingFill(3f, 0f, 100f, 30f));
            Assert.AreEqual(0f, ActionButton.RingFill(-1f, 100f, 100f, 30f));
        }
    }
}
