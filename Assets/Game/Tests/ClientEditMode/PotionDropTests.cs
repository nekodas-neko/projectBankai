using System.IO;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using EntityId = Game.Core.EntityId;

namespace Game.Client.Tests
{
    public class PotionDropTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-potion-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static MasteryProgress Progress(string directory, float chance)
        {
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef { Id = "carrier", Stats = new Stats { MaxHealth = 10f }, PotionChance = chance });
            return new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), null, 5, new RewardBook(classes, new MasteryRules()), null);
        }

        static Snapshot Enemies(float health, int count)
        {
            var snapshot = new Snapshot();
            for (var i = 0; i < count; i++)
            {
                snapshot.Entities.Add(new EntityState { Id = new EntityId(i + 1), Team = Team.Enemies, Health = health, ClassHash = StableId.From("carrier") });
            }

            return snapshot;
        }

        static void Kill(MasteryProgress progress, int count)
        {
            progress.Observe(Enemies(10f, count));
            progress.Observe(Enemies(0f, count));
        }

        [Test]
        public void ACertainChanceGivesAPotionAndSavesItAndSaysSo()
        {
            var progress = Progress(directory, 1f);

            Kill(progress, 1);

            Assert.AreEqual(1, progress.Potions);
            Assert.AreEqual("Found a health potion", progress.Summary());
            Assert.AreEqual(1, new ProfileStore(directory, Starter).Load().Potions);
        }

        [Test]
        public void NoChanceGivesNothingAndTheBagStopsAtItsLimit()
        {
            Kill(Progress(directory, 0f), 3);
            Assert.AreEqual(0, new ProfileStore(directory, Starter).Load().Potions);

            var full = Progress(directory, 1f);
            Kill(full, 9);
            Assert.AreEqual(PlayerProfile.MaxPotions, full.Potions);
        }

        [Test]
        public void DrinkingUsesOneAndKeepsTheCountAcrossARestart()
        {
            var progress = Progress(directory, 1f);
            Kill(progress, 3);

            Assert.IsTrue(progress.TryUsePotion());

            Assert.AreEqual(2, progress.Potions);
            Assert.AreEqual(2, new ProfileStore(directory, Starter).Load().Potions);
            Assert.IsTrue(progress.TryUsePotion());
            Assert.IsTrue(progress.TryUsePotion());
            Assert.IsFalse(progress.TryUsePotion(), "none left");
        }

        [Test]
        public void TheShippedMobsAndBossesCanDropPotions()
        {
            DefinitionLoader.Load(out var classes, out _);

            Assert.Greater(classes.Get("slime").PotionChance, 0f);
            Assert.Less(classes.Get("slime").PotionChance, 0.2f);
            Assert.AreEqual(1f, classes.Get("slime_boss").PotionChance);
            Assert.Greater(classes.Get("cave_crawler").WanderRadius, 0f);
        }

        [Test]
        public void ThePotionButtonSitsAboveTheOtherButtonsWithoutOverlap()
        {
            var hud = Hud.Create(false, new HudCallbacks());
            try
            {
                var potion = (RectTransform)hud.Potion.transform;
                foreach (var other in new[] { hud.Attack, hud.Special, hud.Ultimate, hud.Defense })
                {
                    var rect = (RectTransform)other.transform;
                    Assert.GreaterOrEqual(Vector2.Distance(potion.anchoredPosition, rect.anchoredPosition), (potion.sizeDelta.x + rect.sizeDelta.x) * 0.5f, "the potion button overlaps " + other.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(hud.Root);
                foreach (var system in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                {
                    Object.DestroyImmediate(system.gameObject);
                }
            }
        }
    }
}
