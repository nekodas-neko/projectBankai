using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The small signature of a weapon's default kit, as data on its defensive skill: a block that leaves a short damage cut
    // behind, a dodge that heals a little, a ward that gives mana back (a negative resource cost).
    public class DefenseSignatureTests
    {
        sealed class Setup
        {
            readonly SeededRandom random = new SeededRandom(1);
            readonly LoopbackLink link;
            int sequence;

            public Setup(string playerClass, params string[] npcClasses)
            {
                var abilities = TestData.Abilities();
                abilities.Add(new AbilityDef { Id = "guard_plus", Kind = AbilityKind.Block, Cooldown = 4f, Range = 1f, Duration = 1f, Reduction = 0.6f, Fortify = 0.5f, FortifySeconds = 3f });
                abilities.Add(new AbilityDef { Id = "step_plus", Kind = AbilityKind.Dodge, Cooldown = 3f, Range = 1f, Duration = 0.4f, DashDistance = 1f, SelfHeal = 6f });
                abilities.Add(new AbilityDef { Id = "ward_plus", Kind = AbilityKind.Shield, Cooldown = 5f, Range = 1f, Duration = 3f, Absorb = 30f, ResourceCost = -20f });
                var classes = TestData.Classes();
                classes.Add(new ClassDef { Id = "sentinel", Playable = true, Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f }, AutoAttack = "basic", Defense = "guard_plus" });
                classes.Add(new ClassDef { Id = "skirmisher", Playable = true, Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f }, AutoAttack = "basic", Defense = "step_plus" });
                classes.Add(new ClassDef
                {
                    Id = "acolyte",
                    Playable = true,
                    Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                    AutoAttack = "basic",
                    Defense = "ward_plus",
                    Resource = new ResourceDef { Id = "mana", Max = 100f },
                });
                Server = new GameServer(new ServerConfig(), classes, abilities, random);
                link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), playerClass, link.ServerEnd);
                foreach (var npc in npcClasses)
                {
                    Server.SpawnNpc(npc, Team.Enemies, Player.Position + new Vec2(0f, 2f));
                }
            }

            public GameServer Server { get; }

            public Entity Player { get; }

            public void Defend()
            {
                TestData.Send(link.ClientEnd, ++sequence, CommandType.Defend, Vec2.Zero);
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            // The health lost in each separate hit over the next ticks.
            public List<float> Hits(int ticks)
            {
                var hits = new List<float>();
                for (var i = 0; i < ticks; i++)
                {
                    var before = Player.Health;
                    Server.StepTick();
                    if (Player.Health < before - 1e-4f)
                    {
                        hits.Add(before - Player.Health);
                    }
                }

                return hits;
            }
        }

        [Test]
        public void ABlockLeavesAShortDamageCutBehindAfterItEnds()
        {
            var plain = new Setup("knight", "brute");
            var plus = new Setup("sentinel", "brute");

            plain.Defend();
            plus.Defend();
            var plainHits = plain.Hits(100);
            var plusHits = plus.Hits(100);

            Assert.GreaterOrEqual(plusHits.Count, 2, "the brute swings every two seconds");
            Assert.AreEqual(20f * 0.4f * 0.5f, plusHits[0], 0.01f, "the block cuts 60%, the fortify half of what is left");
            Assert.AreEqual(20f, plainHits[1], 0.01f, "without the fortify the second hit lands in full");
            Assert.AreEqual(20f * 0.5f, plusHits[1], 0.01f, "the block is over but the fortify still cuts the second hit by half");
        }

        [Test]
        public void TheCutEndsWhenItsTimeIsUp()
        {
            var setup = new Setup("sentinel", "brute");

            setup.Defend();
            setup.Tick(1);
            Assert.IsTrue(setup.Player.HasStatus(StatusType.Fortified));
            Assert.AreEqual(0.5f, setup.Player.StatusMagnitude(StatusType.Fortified), 0.001f);

            setup.Tick(95);
            Assert.IsFalse(setup.Player.HasStatus(StatusType.Fortified), "3 seconds after use");
        }

        [Test]
        public void ARedAttackIgnoresTheCutLikeItIgnoresABlock()
        {
            var setup = new Setup("sentinel", "red_brute");

            setup.Defend();
            var hits = setup.Hits(60);

            Assert.IsNotEmpty(hits);
            Assert.AreEqual(20f, hits[0], 0.01f);
        }

        [Test]
        public void ADodgeHealsALittleWhenUsed()
        {
            var setup = new Setup("skirmisher");
            setup.Player.Health = 50f;

            setup.Defend();
            setup.Tick(2);

            Assert.AreEqual(56f, setup.Player.Health, 0.01f);
        }

        [Test]
        public void TheHealStopsAtMaximumHealth()
        {
            var setup = new Setup("skirmisher");
            setup.Player.Health = 98f;

            setup.Defend();
            setup.Tick(2);

            Assert.AreEqual(100f, setup.Player.Health, 0.01f);
        }

        [Test]
        public void AWardWithANegativeCostGivesTheResourceBack()
        {
            var setup = new Setup("acolyte");
            Assert.AreEqual(0f, setup.Player.Resource, 0.001f);

            setup.Defend();
            setup.Tick(2);

            Assert.AreEqual(20f, setup.Player.Resource, 0.001f);
            Assert.IsTrue(setup.Player.HasStatus(StatusType.Shield));
        }

        [Test]
        public void TheValidatorRejectsSignaturesOnAttacksAndBrokenNumbers()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "healing_stab", Cooldown = 1f, Range = 2f, Radius = 1f, Damage = 5f, SelfHeal = 3f });
            abilities.Add(new AbilityDef { Id = "no_time", Kind = AbilityKind.Block, Range = 1f, Duration = 1f, Reduction = 0.5f, Fortify = 0.3f });
            abilities.Add(new AbilityDef { Id = "too_much", Kind = AbilityKind.Block, Range = 1f, Duration = 1f, Reduction = 0.5f, Fortify = 1.5f, FortifySeconds = 2f });
            var classes = TestData.Classes();

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, abilities));

            StringAssert.Contains("'healing_stab': self heal and fortify belong to defensive skills", errors);
            StringAssert.Contains("'no_time': a fortify needs a duration", errors);
            StringAssert.Contains("'too_much': self heal must not be negative, fortify must be between 0 and 1", errors);
        }
    }
}
