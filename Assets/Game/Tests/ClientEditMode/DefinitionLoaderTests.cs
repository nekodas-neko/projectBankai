using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    public class DefinitionLoaderTests
    {
        [Test]
        public void ShippedDefinitions_LoadAndValidate()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            Assert.IsTrue(classes.TryGet("staff", out var mage));
            Assert.IsTrue(mage.Playable);
            Assert.AreEqual("arcane_bolt", mage.AutoAttack);
            CollectionAssert.AreEqual(new[] { "fireball", "meteor_fall" }, mage.Skills);
            Assert.IsTrue(abilities.TryGet("fireball", out var fireball));
            Assert.AreEqual(30f, fireball.Damage);

            Assert.IsTrue(classes.Get("sword_and_shield").Playable);
            Assert.IsTrue(classes.Get("healing_mace").Playable);
            Assert.IsFalse(classes.Get("slime").Playable);
            Assert.IsTrue(classes.TryGet("slime", out _));
            Assert.IsTrue(classes.TryGet("slime_boss", out _));
        }

        [Test]
        public void OnlyTheGroundBombIsRedAndEveryBossMeleeAttackIsOrange()
        {
            DefinitionLoader.Load(out _, out var abilities);

            Assert.IsFalse(abilities.Get("slime_bomb").Avoidable);
            foreach (var id in new[] { "king_smash", "king_slam", "king_wave", "slime_slam", "slime_spit", "sword_slash", "shield_bash", "fireball" })
            {
                Assert.IsTrue(abilities.Get(id).Avoidable, id);
            }
        }

        [Test]
        public void TheShippedBossesDropGroundCirclesOnPlayersAndRedOnesAreRare()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            var spit = abilities.Get("slime_spit");
            Assert.IsTrue(spit.AtTarget);
            Assert.IsTrue(spit.Avoidable, "the everyday ground circle is orange: block, dodge or walk out");
            Assert.Greater(spit.Windup, 0.5f);

            var bomb = abilities.Get("slime_bomb");
            Assert.IsTrue(bomb.AtTarget);
            Assert.IsFalse(bomb.Avoidable);

            var king = classes.Get("slime_boss");
            Assert.AreEqual(MechanicTarget.Farthest, king.Phases[0].Mechanics[0].Target);
            Assert.AreEqual(MechanicTarget.Farthest, king.Phases[1].Mechanics[0].Target);
            Assert.AreEqual("slime_spit", king.Phases[1].Mechanics[0].Ability);
            Assert.GreaterOrEqual(king.Phases[2].Mechanics.Length, 2);

            var mother = classes.Get("brood_mother");
            Assert.AreEqual(MechanicTarget.NotTopThreat, mother.Phases[1].Mechanics[0].Target);
            Assert.GreaterOrEqual(mother.Phases[1].Mechanics[0].IntervalSeconds, 15f, "the red bomb is rare");

            Assert.AreEqual("slime_spit", classes.Get("mushroom_warden").Phases[0].Mechanics[0].Ability);
            Assert.GreaterOrEqual(abilities.Get("king_smash").Cooldown, 12f, "the king's smash is rare");
        }

        [Test]
        public void TheWardenAndTheMotherCastAndTheSwordAndStaffCanInterrupt()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            var devour = abilities.Get("devour");
            Assert.IsTrue(devour.Interruptible);
            Assert.Greater(devour.Heal, 0f);
            Assert.Contains("devour", classes.Get("mushroom_warden").Skills);

            var call = abilities.Get("brood_call");
            Assert.IsTrue(call.Interruptible);
            Assert.AreEqual("slime", call.SummonClass);
            Assert.Greater(call.SummonCount, 0);
            Assert.Contains("brood_call", classes.Get("brood_mother").Skills);

            Assert.Greater(abilities.Get("shield_bash").Interrupt, 0f);
            Assert.Greater(abilities.Get("fireball").Interrupt, 0f);
            Assert.AreEqual(0f, abilities.Get("holy_bolt").Interrupt);
            Assert.AreEqual(0f, abilities.Get("radiant_pulse").Interrupt);
        }

        [Test]
        public void TheShippedBossesPauseAfterEveryAttack()
        {
            DefinitionLoader.Load(out var classes, out _);

            foreach (var id in new[] { "mushroom_warden", "brood_mother", "slime_boss" })
            {
                Assert.Greater(classes.Get(id).RecoverySeconds, 0.5f, id);
            }
        }

        [Test]
        public void SavedClassNamesFromBeforeTheWeaponRenameMapToTheWeapons()
        {
            Assert.AreEqual("sword_and_shield", HudSettings.Migrate("tank"));
            Assert.AreEqual("healing_mace", HudSettings.Migrate("healer"));
            Assert.AreEqual("staff", HudSettings.Migrate("mage"));
            Assert.AreEqual("staff", HudSettings.Migrate("staff"));
        }

        [Test]
        public void EveryWeaponHasANameAndItsSkinsLoadThroughThePrefix()
        {
            var expected = new System.Collections.Generic.Dictionary<string, string>
            {
                { "sword_and_shield", "Sword and shield" },
                { "healing_mace", "Healing mace" },
                { "staff", "Staff" },
            };

            foreach (var pair in expected)
            {
                Assert.AreEqual(pair.Value, ClassCatalog.DisplayName(pair.Key));
                Assert.Greater(SkinCatalog.ForClass(pair.Key).Count, 0, pair.Key);
            }
        }

        [Test]
        public void TheSlimeCaveLoadsWithThreeSectionsEachEndingInABoss()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);

            Assert.IsTrue(dungeons.TryGet("slime_cave", out var cave));
            Assert.AreEqual(3, cave.Rooms.Length);

            var bosses = new[] { "mushroom_warden", "brood_mother", "slime_boss" };
            for (var i = 0; i < cave.Rooms.Length; i++)
            {
                var bossSpawns = System.Array.FindAll(cave.Rooms[i].Spawns, spawn => spawn.Boss);
                Assert.AreEqual(1, bossSpawns.Length, $"section {i + 1} has one boss");
                Assert.AreEqual(bosses[i], bossSpawns[0].Class);
                Assert.Greater(cave.Rooms[i].Spawns.Length, 1, $"section {i + 1} has trash as well");
            }
        }

        [Test]
        public void TheSlimeCavePacksAreNumberedAndTheBossesLeadTheirSections()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);

            var cave = dungeons.Get("slime_cave");
            var packed = 0;
            foreach (var room in cave.Rooms)
            {
                foreach (var spawn in room.Spawns)
                {
                    packed += spawn.Pack > 0 ? 1 : 0;
                    Assert.IsFalse(spawn.Boss && spawn.Pack != 0, "bosses are not part of a pack");
                }
            }

            Assert.Greater(packed, 0);
        }

        [Test]
        public void ShippedVariantsLoadAndTheOnesWithALookHaveItsOwnEffects()
        {
            DefinitionLoader.Load(out _, out var abilities);

            Assert.AreEqual("crimson", abilities.Get("arcane_bolt").Variants[0].Id);
            Assert.AreEqual("frost", abilities.Get("fireball").Variants[0].Id);

            // A variant either has a look of its own (both effects, different from the base) or is numbers only (neither, the base effects are used).
            var variantCount = 0;
            foreach (var ability in Resources.LoadAll<AbilityAsset>(DefinitionAsset.ResourcesFolder))
            {
                foreach (var variant in ability.variants)
                {
                    if (variant.castEffect == null && variant.hitEffect == null)
                    {
                        continue;
                    }

                    variantCount++;
                    Assert.IsNotNull(variant.castEffect, $"{ability.Id}/{variant.id} cast effect");
                    Assert.IsNotNull(variant.hitEffect, $"{ability.Id}/{variant.id} hit effect");
                    Assert.AreNotSame(ability.castEffect, variant.castEffect, $"{ability.Id}/{variant.id} should look different");
                }
            }

            Assert.GreaterOrEqual(variantCount, 4);
        }

        [Test]
        public void LoadedDefinitions_DriveAServer()
        {
            DefinitionLoader.Load(out var classes, out var abilities);
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var client = session.AddPlayer(new PlayerId(1), "staff");
            var slime = session.Server.SpawnNpc("slime", Team.Enemies, new Vec2(-3f, 2f));

            session.Advance(0.2f, 1f / 30f);
            client.Attack(Vec2.Zero);
            session.Advance(0.2f, 1f / 30f);

            var expected = classes.Get("slime").Stats.MaxHealth - abilities.Get("arcane_bolt").Damage * classes.Get("staff").Stats.Power;
            Assert.AreEqual(expected, slime.Health, 0.01f);
        }

        [Test]
        public void EveryAbilityHasEffectsAndEveryClassHasASkin()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            foreach (var ability in abilities.All)
            {
                Assert.IsTrue(AbilityCatalog.TryGet(StableId.From(ability.Id), out var asset), ability.Id);
                Assert.IsNotNull(asset.castEffect, $"{ability.Id} cast effect");
                Assert.IsNotNull(asset.hitEffect, $"{ability.Id} hit effect");
            }

            foreach (var id in new[] { "sword_and_shield", "healing_mace", "staff" })
            {
                Assert.GreaterOrEqual(SkinCatalog.ForClass(id).Count, 2, $"{id} skins");
            }

            Assert.IsNotNull(SkinCatalog.Get(StableId.From("slime_green")));
            Assert.IsNotNull(SkinCatalog.Get(StableId.From("slime_king")));
        }
    }
}
