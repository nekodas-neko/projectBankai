using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TuningTests
    {
        static List<string> Run(string text, Registry<AbilityDef> abilities, Registry<ClassDef> classes)
        {
            var errors = new List<string>();
            var entries = Tuning.Parse(text, errors);
            Tuning.Apply(entries, abilities, classes, errors);
            return errors;
        }

        [Test]
        public void ParseSkipsCommentsAndBlankLinesAndReadsTheParts()
        {
            var errors = new List<string>();

            var entries = Tuning.Parse("# header\n\nability.basic.damage = 12  # a trailing note\r\nclass.fighter.stats.maxHealth=150\n", errors);

            Assert.AreEqual(0, errors.Count);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("ability", entries[0].Kind);
            Assert.AreEqual("basic", entries[0].Id);
            CollectionAssert.AreEqual(new[] { "damage" }, entries[0].Path);
            Assert.AreEqual("12", entries[0].Value);
            Assert.AreEqual(3, entries[0].Line);
            CollectionAssert.AreEqual(new[] { "stats", "maxHealth" }, entries[1].Path);
            Assert.AreEqual("150", entries[1].Value);
        }

        [Test]
        public void ParseReportsLinesItCannotRead()
        {
            var errors = new List<string>();

            Tuning.Parse("nonsense\nweapon.x.y = 1\nability.basic = 3", errors);

            Assert.AreEqual(3, errors.Count);
            StringAssert.Contains("line 1", errors[0]);
            StringAssert.Contains("line 2", errors[1]);
            StringAssert.Contains("line 3", errors[2]);
        }

        [Test]
        public void AFloatAnIntAnEnumAndABoolAreSet()
        {
            var abilities = TestData.Abilities();
            var classes = TestData.Classes();

            var errors = Run("ability.basic.damage = 12.5\nability.basic.shape = cone\nability.basic.angle = 60\nclass.fighter.playable = false", abilities, classes);

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(12.5f, abilities.Get("basic").Damage);
            Assert.AreEqual(AbilityShape.Cone, abilities.Get("basic").Shape);
            Assert.AreEqual(60f, abilities.Get("basic").Angle);
            Assert.IsFalse(classes.Get("fighter").Playable);
        }

        [Test]
        public void NamesIgnoreLetterCase()
        {
            var abilities = TestData.Abilities();

            var errors = Run("ability.basic.DAMAGE = 3\nability.basic.ResourceCost = -4", abilities, TestData.Classes());

            Assert.AreEqual(0, errors.Count);
            Assert.AreEqual(3f, abilities.Get("basic").Damage);
            Assert.AreEqual(-4f, abilities.Get("basic").ResourceCost);
        }

        [Test]
        public void ANumberInsideAStructIsChangedAndWrittenBack()
        {
            var classes = TestData.Classes();

            var errors = Run("class.fighter.stats.maxHealth = 222\nclass.fighter.stats.moveSpeed = 7.5", TestData.Abilities(), classes);

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(222f, classes.Get("fighter").Stats.MaxHealth);
            Assert.AreEqual(7.5f, classes.Get("fighter").Stats.MoveSpeed);
            Assert.AreEqual(1f, classes.Get("fighter").Stats.Power);
        }

        [Test]
        public void AResourceNumberIsChanged()
        {
            var classes = TestData.Classes();

            var errors = Run("class.knight.resource.max = 150\nclass.knight.resource.gainTaken = 0.9", TestData.Abilities(), classes);

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(150f, classes.Get("knight").Resource.Max);
            Assert.AreEqual(0.9f, classes.Get("knight").Resource.GainTaken);
        }

        [Test]
        public void AListEntryIsPickedByIdOrByPosition()
        {
            var abilities = TestData.Abilities();
            var classes = TestData.Classes();

            var errors = Run("ability.spark.variants.hot.damageScale = 3\nability.spark.variants.1.rangeScale = 2\nclass.boss.phases.1.speedScale = 1.7", abilities, classes);

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(3f, abilities.Get("spark").Variants[0].DamageScale);
            Assert.AreEqual(2f, abilities.Get("spark").Variants[1].RangeScale);
            Assert.AreEqual(1.7f, classes.Get("boss").Phases[1].SpeedScale);
        }

        [Test]
        public void UnknownThingsAreReportedWithTheirLine()
        {
            var errors = Run("ability.nope.damage = 1\nclass.nope.stats.maxHealth = 1\nability.basic.nope = 1\nclass.knight.nope.max = 1\nability.spark.variants.zzz.damageScale = 1\nability.spark.variants = 1", TestData.Abilities(), TestData.Classes());

            Assert.AreEqual(6, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("unknown ability 'nope'", errors[0]);
            StringAssert.Contains("unknown class 'nope'", errors[1]);
            StringAssert.Contains("no setting 'nope'", errors[2]);
            StringAssert.Contains("no setting 'nope'", errors[3]);
            StringAssert.Contains("no entry 'zzz'", errors[4]);
            StringAssert.Contains("cannot be set from the file", errors[5]);
        }

        [Test]
        public void ABadValueAnUnsetStructureAndAChangedIdAreRefused()
        {
            var errors = Run("ability.basic.damage = lots\nability.basic.shape = square\nclass.fighter.resource.max = 5\nability.basic.id = other\nability.basic.windup = nan", TestData.Abilities(), TestData.Classes());

            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("not a number", errors[0]);
            StringAssert.Contains("not one of", errors[1]);
            StringAssert.Contains("is not set", errors[2]);
            StringAssert.Contains("id cannot be changed", errors[3]);
            StringAssert.Contains("not a number", errors[4]);
        }

        [Test]
        public void NumbersAreReadTheSameWhateverTheMachineLanguage()
        {
            var current = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var abilities = TestData.Abilities();

                var errors = Run("ability.basic.damage = 12.5", abilities, TestData.Classes());

                Assert.AreEqual(0, errors.Count);
                Assert.AreEqual(12.5f, abilities.Get("basic").Damage);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = current;
            }
        }

        [Test]
        public void AnOverriddenValueStillGoesThroughTheValidator()
        {
            var abilities = TestData.Abilities();
            var classes = TestData.Classes();
            Run("class.fighter.stats.maxHealth = -5", abilities, classes);

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, abilities));

            StringAssert.Contains("'fighter'", errors);
        }
    }
}
