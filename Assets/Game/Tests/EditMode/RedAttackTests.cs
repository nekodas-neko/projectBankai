using NUnit.Framework;

namespace Game.Core.Tests
{
    // Orange attacks can be answered with block, dodge or a shield. Red attacks cannot: you have to be somewhere else.
    public class RedAttackTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Player;
            int sequence;

            public Setup(string brute, string kit = "fighter")
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), kit, Link.ServerEnd);
                Server.SpawnNpc(brute, Team.Enemies, Player.Position + new Vec2(0f, 1.5f));
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public void Send(CommandType type)
            {
                TestData.Send(Link.ClientEnd, ++sequence, type, new Vec2(0f, 1f));
                Server.StepTick();
            }
        }

        [Test]
        public void AnOrangeHitIsReducedByABlock()
        {
            var setup = new Setup("orange_brute");
            setup.Player.AddStatus(StatusType.Blocking, 3f, 0.6f);

            setup.Tick(2);

            Assert.AreEqual(92f, setup.Player.Health, 0.001f);
        }

        [Test]
        public void ARedHitIgnoresABlock()
        {
            var setup = new Setup("red_brute");
            setup.Player.AddStatus(StatusType.Blocking, 3f, 0.6f);

            setup.Tick(2);

            Assert.AreEqual(80f, setup.Player.Health, 0.001f);
        }

        [Test]
        public void AnOrangeHitIsDodgedButARedHitIsNot()
        {
            var orange = new Setup("orange_brute");
            orange.Player.AddStatus(StatusType.Invulnerable, 3f, 0f);
            var red = new Setup("red_brute");
            red.Player.AddStatus(StatusType.Invulnerable, 3f, 0f);

            orange.Tick(2);
            red.Tick(2);

            Assert.AreEqual(100f, orange.Player.Health);
            Assert.AreEqual(80f, red.Player.Health, 0.001f);
        }

        [Test]
        public void AnOrangeHitIsSoakedByAShieldButARedHitGoesThrough()
        {
            var orange = new Setup("orange_brute");
            orange.Player.AddStatus(StatusType.Shield, 3f, 40f);
            var red = new Setup("red_brute");
            red.Player.AddStatus(StatusType.Shield, 3f, 40f);

            orange.Tick(2);
            red.Tick(2);

            Assert.AreEqual(100f, orange.Player.Health);
            Assert.AreEqual(80f, red.Player.Health, 0.001f);
        }

        [Test]
        public void ABlockPressedJustAfterAnOrangeHitStillHelpsButNotAfterARedOne()
        {
            var orange = new Setup("orange_brute", "knight");
            var red = new Setup("red_brute", "knight");
            orange.Tick(1);
            red.Tick(1);
            Assert.AreEqual(80f, orange.Player.Health, 0.001f);
            Assert.AreEqual(80f, red.Player.Health, 0.001f);

            orange.Send(CommandType.Defend);
            red.Send(CommandType.Defend);

            Assert.AreEqual(92f, orange.Player.Health, 0.001f);
            Assert.AreEqual(80f, red.Player.Health, 0.001f);
        }

        [Test]
        public void AbilitiesAreOrangeUnlessMarkedRed()
        {
            Assert.IsTrue(new AbilityDef().Avoidable);
            Assert.IsTrue(TestData.Abilities().Get("basic").Avoidable);
            Assert.IsFalse(TestData.Abilities().Get("red_slam").Avoidable);
        }

        [Test]
        public void ARedVariantStaysRed()
        {
            var ability = TestData.Abilities().Get("red_slam");

            Assert.IsFalse(ability.Clone().Avoidable);
        }
    }
}
