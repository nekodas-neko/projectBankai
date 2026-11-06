using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class FightStatsTests
    {
        static readonly PlayerId One = new PlayerId(1);
        static readonly PlayerId Two = new PlayerId(2);

        static Snapshot Snap(DungeonPhase phase = DungeonPhase.Fighting)
        {
            var snapshot = new Snapshot { Dungeon = phase };
            snapshot.Entities.Add(new EntityState { Id = new EntityId(1), Owner = One, Team = Team.Players, Health = 100f });
            snapshot.Entities.Add(new EntityState { Id = new EntityId(2), Owner = Two, Team = Team.Players, Health = 100f });
            snapshot.Entities.Add(new EntityState { Id = new EntityId(10), Team = Team.Enemies, Health = 100f });
            return snapshot;
        }

        static DamageEvent Hit(int source, int target, float amount, HitOutcome outcome = HitOutcome.Hit)
        {
            return new DamageEvent { Source = new EntityId(source), Target = new EntityId(target), Amount = amount, Outcome = outcome };
        }

        [Test]
        public void DamageHealingAndDamageTakenAreCountedPerPlayer()
        {
            var stats = new FightStats();
            var snapshot = Snap();

            stats.Add(Hit(1, 10, 30f), snapshot);
            stats.Add(Hit(1, 10, 20f, HitOutcome.Blocked), snapshot);
            stats.Add(Hit(2, 1, 40f, HitOutcome.Healed), snapshot);
            stats.Add(Hit(10, 1, 25f), snapshot);
            stats.Add(Hit(10, 1, 0f, HitOutcome.Dodged), snapshot);
            stats.Add(Hit(10, 1, 5f, HitOutcome.Blocked), snapshot);
            stats.Add(Hit(1, 10, 0f, HitOutcome.Interrupted), snapshot);

            var rows = new System.Collections.Generic.Dictionary<int, FightStats.Row>();
            foreach (var row in stats.Rows)
            {
                rows[row.Owner.Value] = row;
            }

            Assert.AreEqual(50f, rows[1].Dealt);
            Assert.AreEqual(30f, rows[1].Taken);
            Assert.AreEqual(1, rows[1].Dodged);
            Assert.AreEqual(1, rows[1].Blocked);
            Assert.AreEqual(1, rows[1].Interrupts);
            Assert.AreEqual(40f, rows[2].Healed);
            Assert.AreEqual(0f, rows[2].Dealt);
        }

        [Test]
        public void HitsBetweenEnemiesAndEnemiesAndAlliesDoNotCountAsDamage()
        {
            var stats = new FightStats();
            var snapshot = Snap();

            stats.Add(Hit(10, 10, 99f), snapshot);
            stats.Add(Hit(1, 2, 99f), snapshot);

            Assert.IsEmpty(stats.Rows);
        }

        [Test]
        public void ANewRunAfterTheEndStartsAFreshCount()
        {
            var stats = new FightStats();
            stats.Step(Snap(), 1f);
            stats.Add(Hit(1, 10, 30f), Snap());
            stats.Step(Snap(DungeonPhase.Victory), 1f);

            Assert.AreEqual(1f, stats.Seconds, "the clock stops at the end");

            stats.Step(Snap(), 0.5f);

            Assert.IsEmpty(stats.Rows);
            Assert.AreEqual(0.5f, stats.Seconds);
        }

        [Test]
        public void TheReportListsTheBestDamageFirstAndMarksTheLocalPlayer()
        {
            var stats = new FightStats();
            var snapshot = Snap();
            stats.Step(snapshot, 10f);
            stats.Add(Hit(2, 10, 300f), snapshot);
            stats.Add(Hit(1, 10, 100f), snapshot);
            stats.Add(Hit(10, 1, 50f, HitOutcome.Blocked), snapshot);

            var lines = stats.Report(One).Split('\n');

            Assert.AreEqual("Run time 0:10", lines[0]);
            StringAssert.StartsWith("Player 2   dealt 300 (30/s)", lines[1]);
            StringAssert.StartsWith("You   dealt 100 (10/s)   healed 0   taken 50", lines[2]);
            StringAssert.Contains("blocks 1", lines[2]);
        }

        [Test]
        public void TheClockShowsMinutesAndSeconds()
        {
            Assert.AreEqual("0:05", FightStats.Clock(5.9f));
            Assert.AreEqual("12:03", FightStats.Clock(723f));
        }
    }
}
