using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Tests
{
    public class HomeScreenTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HomeScreenTest", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        Button Find(string name)
        {
            return root.GetComponentsInChildren<Button>(true).First(button => button.name == name);
        }

        [Test]
        public void OnlyTheDungeonCanBePlayedForNow()
        {
            var available = HomeScreen.Modes.Where(mode => mode.Available).Select(mode => mode.Id).ToList();

            CollectionAssert.AreEqual(new[] { "dungeon" }, available, "dungeons are the only game mode for now");
            CollectionAssert.IsSubsetOf(new[] { "arena", "idle", "tower" }, HomeScreen.Modes.Select(mode => mode.Id).ToList());
        }

        [Test]
        public void EveryModeHasAButtonAndOnlyTheDungeonOneWorks()
        {
            var played = new List<string>();
            var screen = new HomeScreen((RectTransform)root.transform, played.Add, () => { });

            Assert.IsTrue(screen.ModeAvailable("dungeon"));
            Assert.IsFalse(screen.ModeAvailable("arena"));
            Assert.IsFalse(screen.ModeAvailable("idle"));
            Assert.IsFalse(screen.ModeAvailable("tower"));

            Find("Modedungeon").onClick.Invoke();

            CollectionAssert.AreEqual(new[] { "dungeon" }, played);
            Assert.IsFalse(Find("Modetower").interactable);
        }

        [Test]
        public void TheCharacterButtonOpensTheCharacterScreen()
        {
            var opened = 0;
            var screen = new HomeScreen((RectTransform)root.transform, _ => { }, () => opened++);

            Find("OpenCharacter").onClick.Invoke();

            Assert.AreEqual(1, opened);
            Assert.IsNotNull(screen);
        }

        [Test]
        public void TheCardShowsWhatTheCharacterScreenShows()
        {
            var screen = new HomeScreen((RectTransform)root.transform, _ => { }, () => { });

            screen.Show(new CharacterInfo { Weapon = "Staff +5%", Armor = "Armor: Wanderer", Stats = "Health 80", Wallet = "Gold 12   Potions 1/5" });

            Assert.AreEqual("Staff +5%", screen.WeaponText);
            Assert.AreEqual("Armor: Wanderer", screen.ArmorText);
            Assert.AreEqual("Health 80", screen.StatsText);
            Assert.AreEqual("Gold 12   Potions 1/5", screen.WalletText);
        }
    }
}
