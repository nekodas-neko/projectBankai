using NUnit.Framework;

namespace Game.Client.Tests
{
    public class TuningLoaderTests
    {
        [TearDown]
        public void TearDown()
        {
            DefinitionLoader.TuningText = null;
        }

        [Test]
        public void WithoutAFileTheShippedNumbersAreUsedAndNothingIsShown()
        {
            DefinitionLoader.TuningText = string.Empty;

            DefinitionLoader.Load(out var classes, out var abilities);

            Assert.AreEqual(string.Empty, DefinitionLoader.TuningStatus);
            Assert.AreEqual(30f, abilities.Get("fireball").Damage);
            Assert.AreEqual(80f, classes.Get("staff").Stats.MaxHealth);
        }

        [Test]
        public void AnOverrideChangesTheLoadedNumbersAndSaysSo()
        {
            DefinitionLoader.TuningText = "ability.fireball.damage = 45\nclass.staff.stats.maxHealth = 95\n";

            DefinitionLoader.Load(out var classes, out var abilities);

            Assert.AreEqual(45f, abilities.Get("fireball").Damage);
            Assert.AreEqual(95f, classes.Get("staff").Stats.MaxHealth);
            Assert.AreEqual("tuning: 2 overrides", DefinitionLoader.TuningStatus);
        }

        [Test]
        public void ABadFileIsIgnoredAsAWholeAndTheScreenSaysSo()
        {
            DefinitionLoader.TuningText = "ability.fireball.damage = 45\nability.nope.damage = 1\n";
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Tuning file ignored"));

            DefinitionLoader.Load(out _, out var abilities);

            Assert.AreEqual(30f, abilities.Get("fireball").Damage);
            StringAssert.StartsWith("tuning IGNORED", DefinitionLoader.TuningStatus);
        }

        [Test]
        public void AValueThatBreaksTheValidatorIsIgnoredToo()
        {
            DefinitionLoader.TuningText = "class.staff.stats.maxHealth = -10\n";
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Tuning file ignored"));

            DefinitionLoader.Load(out var classes, out _);

            Assert.AreEqual(80f, classes.Get("staff").Stats.MaxHealth);
            StringAssert.StartsWith("tuning IGNORED", DefinitionLoader.TuningStatus);
        }

        [Test]
        public void TheExampleFileInTheDocsIsValidAgainstTheShippedDefinitions()
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "docs", "tuning.example.txt"));
            DefinitionLoader.TuningText = System.IO.File.ReadAllText(path);

            DefinitionLoader.Load(out var classes, out var abilities);

            StringAssert.StartsWith("tuning: ", DefinitionLoader.TuningStatus);
            Assert.AreEqual(26f, abilities.Get("king_smash").Damage);
            Assert.AreEqual(1.4f, classes.Get("slime_boss").Phases[2].SpeedScale);
            Assert.AreEqual(0.9f, abilities.Get("fireball").Variants[0].DamageScale);
        }

        [Test]
        public void TheStatusCounterChangesWhenTheStatusDoes()
        {
            DefinitionLoader.TuningText = string.Empty;
            DefinitionLoader.Load(out _, out _);
            var before = DefinitionLoader.TuningChanges;

            DefinitionLoader.TuningText = "ability.fireball.damage = 45";
            DefinitionLoader.Load(out _, out _);

            Assert.Greater(DefinitionLoader.TuningChanges, before);
        }
    }
}
