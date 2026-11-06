using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BossTests
    {
        static Vec2 PlayerSpot => new Vec2(-3f, 0f);

        static (GameServer server, LoopbackLink link, Entity player, SeededRandom random) Setup(string playerClass)
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            var player = server.AddPlayer(new PlayerId(1), playerClass, link.ServerEnd);
            return (server, link, player, random);
        }

        static void Tick(GameServer server, int count)
        {
            for (var i = 0; i < count; i++)
            {
                server.StepTick();
            }
        }

        [Test]
        public void Circle_HitsEverythingAroundTheCasterIncludingBehind()
        {
            var (server, link, player, _) = Setup("shaman");
            var front = server.SpawnNpc("dummy", Team.Enemies, PlayerSpot + new Vec2(3f, 0f));
            var behind = server.SpawnNpc("dummy", Team.Enemies, PlayerSpot + new Vec2(-3f, 0f));
            var far = server.SpawnNpc("dummy", Team.Enemies, PlayerSpot + new Vec2(6f, 0f));

            TestData.Send(link.ClientEnd, 1, CommandType.Attack, new Vec2(1f, 0f));
            Tick(server, 12);

            Assert.AreEqual(90f, front.Health, 0.001f);
            Assert.AreEqual(90f, behind.Health, 0.001f);
            Assert.AreEqual(100f, far.Health);
        }

        [TestCase(4f, 0f, true)]
        [TestCase(4f, 3f, true)]
        [TestCase(4f, 5f, false)]
        [TestCase(-2f, 0f, false)]
        [TestCase(9f, 0f, false)]
        public void Cone_HitsOnlyInsideItsAngleAndRange(float dx, float dy, bool hit)
        {
            var (server, link, _, _) = Setup("shaman");
            var target = server.SpawnNpc("dummy", Team.Enemies, PlayerSpot + new Vec2(dx, dy));

            TestData.Send(link.ClientEnd, 1, CommandType.Skill1, new Vec2(1f, 0f));
            Tick(server, 2);

            Assert.AreEqual(hit ? 90f : 100f, target.Health, 0.001f);
        }

        [Test]
        public void Boss_EntersEachPhaseOnceAndSpawnsItsMinions()
        {
            var (server, _, _, _) = Setup("fighter");
            var boss = server.SpawnNpc("boss", Team.Enemies, new Vec2(0f, 15f));
            Tick(server, 1);
            var baseline = server.Entities.Count;
            Assert.AreEqual(0, boss.Phase);

            boss.Health = 550f;
            Tick(server, 2);
            Assert.AreEqual(1, boss.Phase);
            Assert.AreEqual(baseline + 2, server.Entities.Count);

            Tick(server, 30);
            Assert.AreEqual(baseline + 2, server.Entities.Count);

            boss.Health = 250f;
            Tick(server, 2);
            Assert.AreEqual(2, boss.Phase);
            Assert.AreEqual(baseline + 3, server.Entities.Count);
        }

        [Test]
        public void Boss_SkippingStraightToALatePhaseStillSpawnsEveryPhasesMinions()
        {
            var (server, _, _, _) = Setup("fighter");
            var boss = server.SpawnNpc("boss", Team.Enemies, new Vec2(0f, 15f));
            Tick(server, 1);
            var baseline = server.Entities.Count;

            boss.Health = 100f;
            Tick(server, 2);

            Assert.AreEqual(2, boss.Phase);
            Assert.AreEqual(baseline + 3, server.Entities.Count);
        }

        [Test]
        public void Boss_FirstPhaseOnlyUsesItsBasicAttack()
        {
            var (server, _, player, _) = Setup("fighter");
            var boss = server.SpawnNpc("boss", Team.Enemies, player.Position + new Vec2(0f, 2f));

            Tick(server, 2);

            Assert.Greater(boss.Cooldowns[boss.SlotOf("basic")], 0f);
            Assert.AreEqual(0f, boss.Cooldowns[boss.SlotOf("nova")]);
        }

        [Test]
        public void Boss_LaterPhaseUsesTheHigherPriorityAttackFirst()
        {
            var (server, _, player, _) = Setup("fighter");
            var boss = server.SpawnNpc("boss", Team.Enemies, player.Position + new Vec2(0f, 2f));
            boss.Health = 550f;

            Tick(server, 2);

            Assert.Greater(boss.Cooldowns[boss.SlotOf("nova")], 0f);
            Assert.AreEqual(0f, boss.Cooldowns[boss.SlotOf("basic")]);
        }

        [Test]
        public void Boss_MovesFasterInItsLastPhase()
        {
            var (server, _, player, _) = Setup("fighter");
            player.Position = new Vec2(0f, -14f);
            var slowBoss = server.SpawnNpc("boss", Team.Enemies, new Vec2(0f, 14f));
            Tick(server, 30);
            var slowMoved = 14f - slowBoss.Position.Y;

            var (server2, _, player2, _) = Setup("fighter");
            player2.Position = new Vec2(0f, -14f);
            var fastBoss = server2.SpawnNpc("boss", Team.Enemies, new Vec2(0f, 14f));
            fastBoss.Health = 100f;
            Tick(server2, 30);
            var fastMoved = 14f - fastBoss.Position.Y;

            Assert.Greater(fastMoved, slowMoved * 1.6f);
        }

        [Test]
        public void Validator_RejectsBrokenShapesAndPhases()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "bad_circle", Shape = AbilityShape.Circle, Range = 1f });
            abilities.Add(new AbilityDef { Id = "bad_cone", Shape = AbilityShape.Cone, Range = 5f });
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef
            {
                Id = "bad_boss",
                Stats = new Stats { MaxHealth = 10f, Power = 1f },
                AutoAttack = "basic",
                AggroRange = 10f,
                Phases = new[]
                {
                    new PhaseDef { HealthBelow = 0.5f, Attacks = new[] { "basic" } },
                    new PhaseDef { HealthBelow = 0.8f, Attacks = new[] { "nova" } },
                    new PhaseDef { HealthBelow = 0.2f, Attacks = new string[0], SpawnClass = "ghost", SpawnCount = 1 },
                },
            });

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, abilities));

            StringAssert.Contains("'bad_circle': a circle needs a radius", errors);
            StringAssert.Contains("'bad_cone': a cone needs an angle", errors);
            StringAssert.Contains("phase 1 health threshold", errors);
            StringAssert.Contains("phase 1 uses 'nova'", errors);
            StringAssert.Contains("phase 2 needs attacks", errors);
            StringAssert.Contains("phase 2 spawns unknown class 'ghost'", errors);
        }
    }
}
