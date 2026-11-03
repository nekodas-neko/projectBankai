using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class MasteryProgressTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-mastery-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static MasteryRules Rules()
        {
            return new MasteryRules { XpPerLevel = new[] { 20, 40 }, XpPerKill = 10, XpPerBossKill = 100 };
        }

        static Snapshot Snap(params (int id, Team team, float health, byte effects)[] entities)
        {
            var snapshot = new Snapshot();
            foreach (var (id, team, health, effects) in entities)
            {
                snapshot.Entities.Add(new EntityState { Id = new EntityId(id), Team = team, Health = health, MaxHealth = 100f, Effects = effects });
            }

            return snapshot;
        }

        MasteryProgress Progress()
        {
            return new MasteryProgress(new ProfileStore(directory, Starter), Rules());
        }

        [Test]
        public void AnEnemyThatFallsIsCountedOnceAndABossCountsAsABoss()
        {
            var tracker = new KillTracker();
            tracker.Observe(Snap((1, Team.Enemies, 50f, 0), (2, Team.Enemies, 80f, EffectFlags.Boss)), out _, out _);

            tracker.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 80f, EffectFlags.Boss)), out var kills, out var bosses);
            Assert.AreEqual(1, kills);
            Assert.AreEqual(0, bosses);

            tracker.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 0f, EffectFlags.Boss)), out kills, out bosses);
            Assert.AreEqual(0, kills, "the first one is not counted again");
            Assert.AreEqual(1, bosses);
        }

        [Test]
        public void EnemiesAlreadyDownWhenTheClientJoinsAreNotCounted()
        {
            var tracker = new KillTracker();

            tracker.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 0f, EffectFlags.Boss)), out var kills, out var bosses);

            Assert.AreEqual(0, kills);
            Assert.AreEqual(0, bosses);
        }

        [Test]
        public void FallenPlayersAndFriendsAreNotKills()
        {
            var tracker = new KillTracker();
            tracker.Observe(Snap((1, Team.Players, 50f, 0), (2, Team.Enemies, 50f, 0)), out _, out _);

            tracker.Observe(Snap((1, Team.Players, 0f, 0), (2, Team.Enemies, 50f, 0)), out var kills, out var bosses);

            Assert.AreEqual(0, kills + bosses);
        }

        [Test]
        public void KillsTrainTheWeaponInHandAndLevelUpsAreReported()
        {
            var progress = Progress();
            progress.Equip("staff");
            progress.Observe(Snap((1, Team.Enemies, 50f, 0), (2, Team.Enemies, 50f, 0)));

            var gained = progress.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 0f, 0)));

            Assert.AreEqual(1, gained, "two kills at 10 XP reach the 20 XP of level 1");
            Assert.AreEqual(20, progress.Profile.MasteryFor("staff").Xp);
            Assert.AreEqual(0, progress.Profile.MasteryFor("sword_and_shield").Xp);
            Assert.AreEqual("Staff mastery level 1!", progress.Summary());
        }

        [Test]
        public void TheSummaryShowsTheBarAndTheFreePoints()
        {
            var progress = Progress();
            progress.Equip("healing_mace");
            progress.Observe(Snap((1, Team.Enemies, 50f, 0)));
            progress.Observe(Snap((1, Team.Enemies, 0f, 0)));

            Assert.AreEqual("Healing mace mastery 0 (10/20)", progress.Summary());

            progress.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 50f, 0)));
            progress.Observe(Snap((1, Team.Enemies, 0f, 0), (2, Team.Enemies, 0f, 0)));
            progress.Tick(6f);

            Assert.AreEqual("Healing mace mastery 1 (0/40), 1 point", progress.Summary());
        }

        [Test]
        public void ABossKillIsSavedAtOnce()
        {
            var progress = Progress();
            progress.Observe(Snap((7, Team.Enemies, 50f, EffectFlags.Boss)));
            progress.Observe(Snap((7, Team.Enemies, 0f, EffectFlags.Boss)));

            Assert.IsFalse(progress.Dirty);
            Assert.AreEqual(60, new ProfileStore(directory, Starter).Load().MasteryFor("sword_and_shield").Xp, "a boss is worth 100 but the two test levels cap at 60");
        }

        [Test]
        public void OrdinaryKillsAreSavedWhenFlushedOrAfterAWhile()
        {
            var progress = Progress();
            progress.Observe(Snap((1, Team.Enemies, 50f, 0)));
            progress.Observe(Snap((1, Team.Enemies, 0f, 0)));

            Assert.IsTrue(progress.Dirty, "one kill does not level up, so it waits");
            progress.Tick(25f);
            Assert.IsFalse(progress.Dirty);
            Assert.AreEqual(10, new ProfileStore(directory, Starter).Load().MasteryFor("sword_and_shield").Xp);
        }

        [Test]
        public void EquippingTheWeaponAlreadyInHandChangesNothingToSave()
        {
            var progress = Progress();

            progress.Equip("sword_and_shield");
            Assert.IsFalse(progress.Dirty, "starting the game must not rewrite the profile");

            progress.Equip("staff");
            Assert.IsTrue(progress.Dirty);
        }

        [Test]
        public void ProgressSurvivesARestartOfTheGame()
        {
            var first = Progress();
            first.Equip("staff");
            first.Observe(Snap((7, Team.Enemies, 50f, EffectFlags.Boss)));
            first.Observe(Snap((7, Team.Enemies, 0f, EffectFlags.Boss)));

            var second = Progress();
            second.Equip("staff");

            Assert.AreEqual(2, second.Level());
            Assert.AreEqual("Staff", MasteryProgress.Name("staff"));
        }
    }
}
