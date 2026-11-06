using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class QuestLogTests
    {
        static DungeonDef Dungeon()
        {
            return new DungeonDef
            {
                Id = "test",
                Rooms = new[]
                {
                    new RoomDef { Spawns = new[] { new SpawnDef { Class = "mushroom_warden", Boss = true }, new SpawnDef { Class = "slime" } } },
                    new RoomDef { Spawns = new[] { new SpawnDef { Class = "brood_mother", Boss = true } } },
                    new RoomDef { Name = "the vault", Spawns = new[] { new SpawnDef { Class = "slime" } } },
                },
            };
        }

        static Snapshot At(int room, DungeonPhase phase = DungeonPhase.Fighting)
        {
            return new Snapshot { Room = (byte)room, RoomCount = 3, Dungeon = phase };
        }

        [Test]
        public void EachSectionIsOneStepNamedAfterItsBoss()
        {
            var entries = QuestLog.Build(Dungeon(), At(1));

            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual("Defeat the Mushroom Warden", entries[0].Text);
            Assert.AreEqual("Defeat the Brood Mother", entries[1].Text);
            Assert.AreEqual("Clear the vault", entries[2].Text, "a section without a boss is cleared instead");
        }

        [Test]
        public void TheCurrentSectionIsActiveTheOnesBehindAreDoneAndTheRestAreLocked()
        {
            var entries = QuestLog.Build(Dungeon(), At(2));

            Assert.AreEqual(QuestState.Done, entries[0].State);
            Assert.AreEqual(QuestState.Active, entries[1].State);
            Assert.AreEqual(QuestState.Locked, entries[2].State);
            Assert.AreEqual("Defeat the Brood Mother", QuestLog.Current(entries));
        }

        [Test]
        public void VictoryTicksEverythingAndTheTopLineSaysTheDungeonIsComplete()
        {
            var entries = QuestLog.Build(Dungeon(), At(3, DungeonPhase.Victory));

            Assert.IsTrue(entries.TrueForAll(entry => entry.State == QuestState.Done));
            Assert.AreEqual("Dungeon complete", QuestLog.Current(entries));
        }

        [Test]
        public void WithoutASnapshotTheFirstStepIsTheTask()
        {
            var entries = QuestLog.Build(Dungeon(), null);

            Assert.AreEqual(QuestState.Active, entries[0].State);
            Assert.AreEqual(QuestState.Locked, entries[1].State);
        }

        [Test]
        public void NoDungeonMeansNoLog()
        {
            Assert.IsEmpty(QuestLog.Build(null, At(1)));
            Assert.IsEmpty(QuestLog.Build(new DungeonDef { Id = "empty" }, At(1)));
            Assert.AreEqual(string.Empty, QuestLog.Current(QuestLog.Build(null, null)));
        }

        [Test]
        public void TheFormattedLogMarksDoneActiveAndLockedSteps()
        {
            var text = QuestLog.Format(QuestLog.Build(Dungeon(), At(2)));

            StringAssert.Contains("[x] Defeat the Mushroom Warden", text);
            StringAssert.Contains("> Defeat the Brood Mother", text);
            StringAssert.Contains("[ ] Clear the vault", text);
        }

        [Test]
        public void ClassIdsBecomeReadableNames()
        {
            Assert.AreEqual("Slime Boss", QuestLog.Title("slime_boss"));
            Assert.AreEqual("Warden", QuestLog.Title("warden"));
            Assert.AreEqual(string.Empty, QuestLog.Title(null));
        }

        [Test]
        public void TheShippedCaveHasAStepForEachBoss()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);

            var entries = QuestLog.Build(dungeons.Get("slime_cave"), At(1));

            Assert.AreEqual(3, entries.Count);
            foreach (var entry in entries)
            {
                StringAssert.StartsWith("Defeat the", entry.Text);
            }
        }
    }
}
