using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A stunning hit freezes a small enemy for a few seconds, and an idle enemy can stroll about instead of standing still.
    public class StunAndWanderTests
    {
        static readonly Vec2 Home = new Vec2(12f, 12f);

        static (Registry<ClassDef> classes, Registry<AbilityDef> abilities) Data()
        {
            var classes = TestData.Classes();
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "stunner", Shape = AbilityShape.Circle, Cooldown = 1f, Range = 3f, Radius = 3f, Damage = 1f, Stun = 3f });
            abilities.Add(new AbilityDef { Id = "slow_slam", Shape = AbilityShape.Circle, Cooldown = 5f, Range = 3f, Radius = 3f, Damage = 20f, Windup = 1.5f });
            classes.Add(new ClassDef { Id = "slow_brute", Stats = new Stats { MaxHealth = 100f, MoveSpeed = 3f, Power = 1f }, AutoAttack = "slow_slam", AggroRange = 15f });
            classes.Add(new ClassDef { Id = "bruiser", Playable = true, Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 1f }, AutoAttack = "stunner" });
            classes.Add(new ClassDef { Id = "wanderer", Stats = new Stats { MaxHealth = 50f, MoveSpeed = 3f, Power = 1f }, AutoAttack = "basic", AggroRange = 5f, LeashRange = 14f, WanderRadius = 6f });
            return (classes, abilities);
        }

        static LocalSession Session(out GameClient client, ulong seed = 1)
        {
            var (classes, abilities) = Data();
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            client = session.AddPlayer(new PlayerId(1), "bruiser");
            return session;
        }

        [Test]
        public void AStunningHitFreezesASmallEnemyForAWhile()
        {
            var session = Session(out var client);
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var grunt = session.Server.SpawnNpc("grunt", Team.Enemies, player.Position + new Vec2(0f, 2f));

            client.Attack(new Vec2(0f, 1f));
            session.Advance(0.1f, 1f / 30f);

            Assert.IsTrue(grunt.HasStatus(StatusType.Staggered));
            var spot = grunt.Position;
            session.Advance(2f, 1f / 30f);
            Assert.AreEqual(spot.X, grunt.Position.X, 0.001f, "it did not move");
            Assert.AreEqual(spot.Y, grunt.Position.Y, 0.001f);
            Assert.AreEqual(100f, player.Health, 0.001f, "and did not hit back");

            session.Advance(1.5f, 1f / 30f);
            Assert.IsFalse(grunt.HasStatus(StatusType.Staggered), "the stun wore off");
        }

        [Test]
        public void ABossIgnoresTheStun()
        {
            var session = Session(out var client);
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var boss = session.Server.SpawnNpc("grunt", Team.Enemies, player.Position + new Vec2(0f, 2f));
            boss.IsBoss = true;

            client.Attack(new Vec2(0f, 1f));
            session.Advance(0.1f, 1f / 30f);

            Assert.IsFalse(boss.HasStatus(StatusType.Staggered));
        }

        [Test]
        public void AStunCancelsTheAttackAnEnemyWasWindingUp()
        {
            var session = Session(out var client);
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var lurcher = session.Server.SpawnNpc("slow_brute", Team.Enemies, player.Position + new Vec2(0f, 2f));
            for (var i = 0; i < 90 && lurcher.Winding <= 0f; i++)
            {
                session.Advance(1f / 30f, 1f / 30f);
            }

            Assert.Greater(lurcher.Winding, 0f, "it started a slow attack");
            var before = player.Health;

            client.Attack(new Vec2(0f, 1f));
            session.Advance(0.1f, 1f / 30f);
            Assert.AreEqual(0f, lurcher.Winding, 0.001f);
            session.Advance(1.5f, 1f / 30f);

            Assert.AreEqual(before, player.Health, 0.001f, "the cancelled attack never landed");
        }

        [Test]
        public void ANegativeStunIsRefusedByTheValidator()
        {
            var (classes, abilities) = Data();
            abilities.Add(new AbilityDef { Id = "bad_stun", Cooldown = 1f, Range = 3f, Radius = 0.5f, Damage = 1f, Stun = -1f });

            Assert.IsNotEmpty(DefinitionValidator.Validate(classes, abilities));
        }

        static List<Vec2> Walk(ulong seed, float seconds, float radius = 6f)
        {
            var (classes, abilities) = Data();
            classes.Get("wanderer").WanderRadius = radius;
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            session.AddPlayer(new PlayerId(1), "fighter");
            var wanderer = session.Server.SpawnNpc("wanderer", Team.Enemies, Home);
            var path = new List<Vec2>();
            for (var i = 0; i < seconds * 30; i++)
            {
                session.Advance(1f / 30f, 1f / 30f);
                path.Add(wanderer.Position);
            }

            return path;
        }

        [Test]
        public void AnIdleWandererStrollsButStaysNearHome()
        {
            var path = Walk(1, 40f);

            var farthest = 0f;
            var moved = false;
            foreach (var spot in path)
            {
                var offset = spot - Home;
                farthest = System.Math.Max(farthest, offset.Length);
                moved |= offset.Length > 0.5f;
            }

            Assert.IsTrue(moved, "it wandered");
            Assert.LessOrEqual(farthest, 6.5f, "and stayed inside its radius");
        }

        [Test]
        public void WithoutAWanderRadiusAnEnemyStandsStill()
        {
            foreach (var spot in Walk(1, 10f, 0f))
            {
                Assert.AreEqual(Home.Y, spot.Y, 0.001f);
                Assert.AreEqual(Home.X, spot.X, 0.001f);
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameWalk()
        {
            var a = Walk(7, 12f);
            var b = Walk(7, 12f);

            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 0.0001f);
                Assert.AreEqual(a[i].Y, b[i].Y, 0.0001f);
            }
        }

        [Test]
        public void AWandererThatNoticesAPlayerChasesInsteadOfStrolling()
        {
            var (classes, abilities) = Data();
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            session.AddPlayer(new PlayerId(1), "fighter");
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var wanderer = session.Server.SpawnNpc("wanderer", Team.Enemies, player.Position + new Vec2(0f, 4f));

            session.Advance(1f, 1f / 30f);

            Assert.AreEqual(player.Id, wanderer.Target);
        }

        [Test]
        public void AWandererNeedsAnAggroRange()
        {
            var (classes, abilities) = Data();
            classes.Add(new ClassDef { Id = "lost", Stats = new Stats { MaxHealth = 10f, MoveSpeed = 3f, Power = 1f }, AutoAttack = "basic", WanderRadius = 5f });

            Assert.IsNotEmpty(DefinitionValidator.Validate(classes, abilities));
        }
    }
}
