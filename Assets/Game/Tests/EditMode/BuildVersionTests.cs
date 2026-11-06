using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BuildVersionTests
    {
        [Test]
        public void Format_WithCountAndHash_JoinsBoth()
        {
            Assert.AreEqual("b42-a1b2c3d", BuildVersion.Format(42, "a1b2c3d"));
        }

        [Test]
        public void Format_WithoutHash_ShowsCountOnly()
        {
            Assert.AreEqual("b7", BuildVersion.Format(7, null));
            Assert.AreEqual("b7", BuildVersion.Format(7, ""));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Format_WithoutCount_ReturnsUnknown(int count)
        {
            Assert.AreEqual(BuildVersion.Unknown, BuildVersion.Format(count, "a1b2c3d"));
        }
    }
}
