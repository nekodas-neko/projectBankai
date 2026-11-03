using NUnit.Framework;

namespace Game.Core.Tests
{
    // Casts: a boss that stands still while it winds up can be interrupted, and the adds it summons go for the farthest player.
    public class InterruptTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Player;
            public readonly Entity Boss;
            int sequence;

            public Setup(string boss, string player = "kicker")
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), player, Link.ServerEnd);
                Player.Position = new Vec2(0f, 0f);
                Boss = Server.SpawnNpc(boss, Team.Enemies, new Vec2(0f, 2f));
            }

            public void Kick()
            {
                TestData.Send(Link.ClientEnd, ++sequence, CommandType.Attack, new Vec2(0f, 1f));
            }

            public void Run(float seconds)
            {
                for (var i = 0; i < (int)(seconds * 30f); i++)
                {
                    Server.StepTick();
                }
            }

            public bool RunUntilCasting()
            {
                for (var i = 0; i < 30 && Boss.Winding <= 0f; i++)
                {
                    Server.StepTick();
                }

                return Boss.Winding > 0f;
            }
        }

        [Test]
        public void AnUninterruptedCastLands()
        {
            var setup = new Setup("mender");
            setup.Boss.Health = 100f;

            Assert.IsTrue(setup.RunUntilCasting());
            setup.Run(2f);

            Assert.AreEqual(160f, setup.Boss.Health, 0.01f, "the heal went off");
        }

        [Test]
        public void AnInterruptHitCancelsTheCastAndStaggersTheCaster()
        {
            var setup = new Setup("mender");
            setup.Boss.Health = 100f;
            Assert.IsTrue(setup.RunUntilCasting());

            setup.Kick();
            setup.Run(0.2f);

            Assert.AreEqual(0f, setup.Boss.Winding, 0.001f);
            Assert.IsTrue(setup.Boss.HasStatus(StatusType.Staggered));
            Assert.AreEqual(EffectFlags.Staggered, setup.Boss.EffectFlagsNow() & EffectFlags.Staggered);
            setup.Run(1.5f);
            Assert.LessOrEqual(setup.Boss.Health, 100f, "the cancelled heal never landed");
        }

        [Test]
        public void AStaggeredCasterDoesNothingUntilItWearsOff()
        {
            var setup = new Setup("mender");
            Assert.IsTrue(setup.RunUntilCasting());
            setup.Kick();
            setup.Run(0.1f);

            for (var i = 0; i < 45; i++)
            {
                setup.Server.StepTick();
                Assert.AreEqual(0f, setup.Boss.Winding, 0.001f, "no new cast while staggered");
            }

            setup.Run(1f);
            Assert.IsFalse(setup.Boss.HasStatus(StatusType.Staggered), "the stagger wore off");
        }

        [Test]
        public void ACastThatIsNotInterruptibleIgnoresAnInterruptHit()
        {
            var setup = new Setup("red_warden");
            Assert.IsTrue(setup.RunUntilCasting());

            setup.Kick();
            setup.Run(0.2f);

            Assert.Greater(setup.Boss.Winding, 0f, "the swing carried on");
            Assert.IsFalse(setup.Boss.HasStatus(StatusType.Staggered));
        }

        [Test]
        public void SummonedAddsAppearAroundTheCasterAndGoForTheFarthestPlayer()
        {
            var setup = new Setup("summoner");
            var random = new SeededRandom(2);
            var far = setup.Server.AddPlayer(new PlayerId(2), "kicker", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
            far.Position = new Vec2(0f, -9f);

            Assert.IsTrue(setup.RunUntilCasting());
            setup.Run(1.2f);

            var adds = 0;
            foreach (var entity in setup.Server.Entities)
            {
                if (entity.Summoned)
                {
                    adds++;
                    Assert.AreEqual(far.Id, entity.ForcedTarget, "sent at the farthest player");
                    Assert.Greater(entity.ForcedSeconds, 5f);
                    Assert.AreEqual(EffectFlags.Summoned, entity.EffectFlagsNow() & EffectFlags.Summoned, "clients can tell an add from a real enemy");
                }
                else
                {
                    Assert.AreEqual(0, entity.EffectFlagsNow() & EffectFlags.Summoned);
                }
            }

            Assert.AreEqual(2, adds);
        }

        [Test]
        public void AnInterruptedSummonBringsNoAdds()
        {
            var setup = new Setup("summoner");
            Assert.IsTrue(setup.RunUntilCasting());

            setup.Kick();
            setup.Run(2f);

            foreach (var entity in setup.Server.Entities)
            {
                Assert.IsFalse(entity.Summoned);
            }
        }

        [Test]
        public void ATauntPullsTheAddsOffTheirTarget()
        {
            var setup = new Setup("summoner", "taunter");
            var random = new SeededRandom(2);
            var far = setup.Server.AddPlayer(new PlayerId(2), "kicker", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
            far.Position = new Vec2(0f, -9f);
            Assert.IsTrue(setup.RunUntilCasting());
            setup.Run(1.2f);

            Entity add = null;
            foreach (var entity in setup.Server.Entities)
            {
                if (entity.Summoned)
                {
                    add = entity;
                }
            }

            Assert.IsNotNull(add);
            Assert.AreEqual(far.Id, add.ForcedTarget);
            add.Position = new Vec2(0f, 1f);

            setup.Kick();
            setup.Run(0.2f);

            Assert.AreEqual(setup.Player.Id, add.ForcedTarget, "the tank's taunt now holds the add");
        }

        [Test]
        public void TheValidatorRefusesAnInterruptibleGroundCircle()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "bad_cast", Shape = AbilityShape.Circle, Radius = 2f, Windup = 1f, Interruptible = true, AtTarget = true });

            var errors = DefinitionValidator.Validate(TestData.Classes(), abilities);

            Assert.IsTrue(errors.Exists(e => e.Contains("bad_cast")), string.Join("\n", errors));
        }
    }
}
