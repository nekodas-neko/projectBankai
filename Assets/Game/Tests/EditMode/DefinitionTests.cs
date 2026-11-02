using System;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DefinitionTests
    {
        [Test]
        public void TestData_IsValid()
        {
            Assert.IsEmpty(DefinitionValidator.Validate(TestData.Classes(), TestData.Abilities()));
        }

        [Test]
        public void Registry_RejectsDuplicateAndMissingIds()
        {
            var registry = TestData.Abilities();

            Assert.Throws<ArgumentException>(() => registry.Add(new AbilityDef { Id = "basic" }));
            Assert.Throws<ArgumentException>(() => registry.Add(new AbilityDef { Id = "" }));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => registry.Get("nope"));
            Assert.IsFalse(registry.TryGet(null, out _));
        }

        [Test]
        public void Validator_ReportsEveryProblem()
        {
            var abilities = new Registry<AbilityDef>();
            abilities.Add(new AbilityDef { Id = "bad", Cooldown = -1f, Range = 0f, Radius = -1f, Damage = -5f });
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef
            {
                Id = "broken",
                Stats = new Stats { MaxHealth = 0f, MoveSpeed = -1f, Power = 1f },
                AutoAttack = "missing",
                Skills = new[] { "bad", "bad", "bad", "ghost" },
            });

            var errors = DefinitionValidator.Validate(classes, abilities);

            Assert.AreEqual(9, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("missing", string.Join("\n", errors));
            StringAssert.Contains("ghost", string.Join("\n", errors));
        }
    }
}
