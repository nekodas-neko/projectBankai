using NUnit.Framework;

namespace Game.Core.Tests
{
    // A boss mechanic on a timer: a ground circle dropped on a player chosen by a rule, shown as a warning first.
    public class BossMechanicsTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly Registry<ClassDef> Classes;
            public readonly Entity Near;
            public readonly Entity Far;
            public readonly Entity Boss;
            readonly LoopbackLink link;

            public Setup(MechanicTarget rule = MechanicTarget.Farthest, int count = 1, float interval = 4f)
            {
                Classes = TestData.Classes();
                var mechanic = Classes.Get("bomber").Phases[0].Mechanics[0];
                mechanic.Target = rule;
                mechanic.Count = count;
                mechanic.IntervalSeconds = interval;

                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), Classes, TestData.Abilities(), random);
                link = new LoopbackLink(random, NetworkConditions.None);
                Near = Server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd);
                Far = Server.AddPlayer(new PlayerId(2), "fighter", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
                Boss = Server.SpawnNpc("bomber", Team.Enemies, new Vec2(0f, 10f));
                Near.Position = new Vec2(0f, 6f);
                Far.Position = new Vec2(0f, -8f);
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public Snapshot Latest()
            {
                Snapshot latest = null;
                while (link.ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot))
                    {
                        latest = snapshot;
                    }
                }

                return latest;
            }
        }

        [Test]
        public void NothingHappensBeforeTheFirstDelay()
        {
            var setup = new Setup();

            setup.Tick(20);

            Assert.AreEqual(0, setup.Latest().Windups.Count);
        }

        [Test]
        public void AfterTheFirstDelayAWarningAppearsOnTheGroundAtTheFarthestPlayer()
        {
            var setup = new Setup();

            setup.Tick(40);
            var windups = setup.Latest().Windups;

            Assert.AreEqual(1, windups.Count);
            Assert.AreEqual(setup.Far.Position.X, windups[0].Origin.X, 0.01f);
            Assert.AreEqual(setup.Far.Position.Y, windups[0].Origin.Y, 0.01f);
            Assert.AreEqual(setup.Boss.Id, windups[0].User);
        }

        [Test]
        public void ThePlayerWhoStaysTakesTheHitAndThePlayerWhoLeavesDoesNot()
        {
            var setup = new Setup();
            setup.Tick(40);
            Assert.AreEqual(1, setup.Latest().Windups.Count);

            setup.Far.Position = new Vec2(8f, -8f);
            setup.Tick(40);

            Assert.AreEqual(100f, setup.Far.Health);

            var other = new Setup();
            other.Tick(40);
            other.Tick(40);

            Assert.AreEqual(75f, other.Far.Health, 0.01f);
        }

        [Test]
        public void ARedGroundCircleCannotBeBlockedOrDodged()
        {
            var setup = new Setup();
            setup.Tick(40);
            setup.Far.AddStatus(StatusType.Invulnerable, 3f, 0f);
            setup.Far.AddStatus(StatusType.Blocking, 3f, 0.9f);

            setup.Tick(40);

            Assert.AreEqual(75f, setup.Far.Health, 0.01f);
        }

        [Test]
        public void TheBossKeepsFightingNormallyWhileACircleWindsUp()
        {
            var setup = new Setup();

            setup.Tick(40);

            Assert.AreEqual(0f, setup.Boss.Winding);
            Assert.AreEqual(1, setup.Latest().Windups.Count);
        }

        [Test]
        public void TheMechanicRepeatsOnItsInterval()
        {
            var setup = new Setup(interval: 3f);
            var starts = 0;
            var previous = 0;

            for (var tick = 0; tick < 30 * 9; tick++)
            {
                setup.Tick(1);
                var current = setup.Latest()?.Windups.Count ?? previous;
                if (current > previous)
                {
                    starts++;
                }

                previous = current;
            }

            Assert.GreaterOrEqual(starts, 3);
        }

        [Test]
        public void TheNotTopThreatRuleSkipsTheMainTarget()
        {
            var setup = new Setup(MechanicTarget.NotTopThreat);
            setup.Boss.AddThreat(setup.Far.Id, 500f);

            setup.Tick(40);
            var windups = setup.Latest().Windups;

            Assert.AreEqual(1, windups.Count);
            Assert.AreEqual(setup.Near.Position.Y, windups[0].Origin.Y, 0.01f);
        }

        [Test]
        public void TheRandomRuleReachesBothPlayersOverTime()
        {
            var setup = new Setup(MechanicTarget.Random, 1, 1.5f);
            var hitNear = false;
            var hitFar = false;

            for (var tick = 0; tick < 30 * 40 && !(hitNear && hitFar); tick++)
            {
                setup.Tick(1);
                var windups = setup.Latest()?.Windups;
                for (var i = 0; windups != null && i < windups.Count; i++)
                {
                    hitNear |= windups[i].Origin.Y > 0f;
                    hitFar |= windups[i].Origin.Y < 0f;
                }
            }

            Assert.IsTrue(hitNear && hitFar);
        }

        [Test]
        public void ACastOfTwoPicksTwoDifferentPlayers()
        {
            var setup = new Setup(MechanicTarget.Farthest, 2);

            setup.Tick(40);
            var windups = setup.Latest().Windups;

            Assert.AreEqual(2, windups.Count);
            Assert.AreNotEqual(windups[0].Origin.Y, windups[1].Origin.Y);
        }

        [Test]
        public void AMechanicTargetsOnlyLivingPlayers()
        {
            var setup = new Setup();
            setup.Far.Health = 0f;

            setup.Tick(40);
            var windups = setup.Latest().Windups;

            Assert.AreEqual(1, windups.Count);
            Assert.AreEqual(setup.Near.Position.Y, windups[0].Origin.Y, 0.01f);
        }

        [Test]
        public void AbilitiesAreNotGroundTargetedByDefault()
        {
            Assert.IsFalse(new AbilityDef().AtTarget);
            Assert.IsFalse(TestData.Abilities().Get("basic").AtTarget);
            Assert.IsTrue(TestData.Abilities().Get("bomb").AtTarget);
        }

        [Test]
        public void Validator_RejectsBrokenMechanics()
        {
            var classes = TestData.Classes();
            classes.Add(new ClassDef
            {
                Id = "broken_boss",
                Stats = new Stats { MaxHealth = 10f, MoveSpeed = 1f, Power = 1f },
                AutoAttack = "basic",
                AggroRange = 5f,
                Phases = new[]
                {
                    new PhaseDef
                    {
                        Attacks = new[] { "basic" },
                        Mechanics = new[]
                        {
                            new MechanicDef { Ability = "bomb" },
                            new MechanicDef { Ability = "basic", IntervalSeconds = 0f },
                            new MechanicDef { Ability = "basic", Count = 9 },
                            new MechanicDef { Ability = "basic" },
                            new MechanicDef { Ability = "basic" },
                        },
                    },
                },
            });

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, TestData.Abilities()));

            StringAssert.Contains("mechanic uses 'bomb', which is not one of its attacks", errors);
            StringAssert.Contains("positive interval", errors);
            StringAssert.Contains("5 mechanics, the limit is 4", errors);
        }

        [Test]
        public void AMechanicCanBeTunedFromTheTuningFile()
        {
            var classes = TestData.Classes();
            var errors = new System.Collections.Generic.List<string>();
            var entries = Tuning.Parse("class.bomber.phases.0.mechanics.0.intervalSeconds = 2.5\nclass.bomber.phases.0.mechanics.0.target = random\nability.bomb.atTarget = false", errors);
            var abilities = TestData.Abilities();

            Tuning.Apply(entries, abilities, classes, errors);

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(2.5f, classes.Get("bomber").Phases[0].Mechanics[0].IntervalSeconds);
            Assert.AreEqual(MechanicTarget.Random, classes.Get("bomber").Phases[0].Mechanics[0].Target);
            Assert.IsFalse(abilities.Get("bomb").AtTarget);
        }
    }
}
