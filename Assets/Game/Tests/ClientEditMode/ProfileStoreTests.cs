using System.IO;
using Game.Client;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Client.Tests
{
    public class ProfileStoreTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-profile-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ANewDeviceGetsTheStarterWeapons()
        {
            var profile = new ProfileStore(directory, Starter).Load();

            Assert.AreEqual(3, profile.Weapons.Count);
            Assert.AreEqual("sword_and_shield", profile.EquippedType);
        }

        [Test]
        public void WhatWasSavedComesBack()
        {
            var store = new ProfileStore(directory, Starter);
            var profile = store.Load();
            profile.Equip("staff");
            profile.MasteryFor("staff").AddXp(new MasteryRules(), 250);

            Assert.IsTrue(store.Save(profile));
            var loaded = new ProfileStore(directory, Starter).Load();

            Assert.AreEqual("staff", loaded.EquippedType);
            Assert.AreEqual(250, loaded.MasteryFor("staff").Xp);
            Assert.IsFalse(File.Exists(store.Path + ".tmp"), "no temporary file is left behind");
        }

        [Test]
        public void SavingTwiceReplacesTheFile()
        {
            var store = new ProfileStore(directory, Starter);
            var profile = store.Load();
            store.Save(profile);
            profile.MasteryFor("staff").AddXp(new MasteryRules(), 50);

            Assert.IsTrue(store.Save(profile));

            Assert.AreEqual(50, store.Load().MasteryFor("staff").Xp);
        }

        [Test]
        public void ADamagedFileIsKeptAsideAndTheCharacterStartsOver()
        {
            Directory.CreateDirectory(directory);
            var store = new ProfileStore(directory, Starter);
            File.WriteAllText(store.Path, "this is not a profile");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Profile file unreadable"));

            var profile = store.Load();

            Assert.AreEqual(3, profile.Weapons.Count);
            Assert.IsFalse(File.Exists(store.Path));
            Assert.AreEqual("this is not a profile", File.ReadAllText(Path.Combine(directory, ProfileStore.DamagedFileName)));
        }

        [Test]
        public void TheShippedMasteryRulesLoadAndAreValid()
        {
            var rules = MasteryCatalog.Load();

            Assert.IsEmpty(rules.Validate());
            Assert.AreEqual(20, rules.MaxLevel);
            Assert.AreEqual(1, rules.PointsPerLevel);
            Assert.Greater(rules.XpPerBossKill, rules.XpPerKill);
        }
    }
}
