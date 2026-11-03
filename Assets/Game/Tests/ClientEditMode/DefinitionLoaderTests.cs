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
        public void TheSmashIsRedAndTheRestOfTheBossAttacksAreOrange()
        {
            DefinitionLoader.Load(out _, out var abilities);

            Assert.IsFalse(abilities.Get("king_smash").Avoidable);
            foreach (var id in new[] { "king_slam", "king_wave", "slime_slam", "sword_slash", "shield_bash", "fireball" })
            {
                Assert.IsTrue(abilities.Get(id).Avoidable, id);
            }
        }

        [Test]
        public void TheShippedBossesDropRedGroundCirclesOnPlayers()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            var bomb = abilities.Get("slime_bomb");
            Assert.IsTrue(bomb.AtTarget);
            Assert.IsFalse(bomb.Avoidable);
            Assert.Greater(bomb.Windup, 1f);

            var king = classes.Get("slime_boss");
            Assert.AreEqual(0, king.Phases[0].Mechanics.Length);
            Assert.AreEqual(MechanicTarget.Farthest, king.Phases[1].Mechanics[0].Target);
            Assert.AreEqual("slime_bomb", king.Phases[1].Mechanics[0].Ability);
            Assert.GreaterOrEqual(king.Phases[2].Mechanics.Length, 2);

            var mother = classes.Get("brood_mother");
            Assert.AreEqual(MechanicTarget.NotTopThreat, mother.Phases[1].Mechanics[0].Target);

            Assert.AreEqual(0, classes.Get("mushroom_warden").Phases[0].Mechanics.Length);
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
        public void ShippedVariantsLoadAndEachHasItsOwnEffects()
        {
            DefinitionLoader.Load(out _, out var abilities);

            Assert.AreEqual("crimson", abilities.Get("arcane_bolt").Variants[0].Id);
            Assert.AreEqual("frost", abilities.Get("fireball").Variants[0].Id);

            var variantCount = 0;
            foreach (var ability in Resources.LoadAll<AbilityAsset>(DefinitionAsset.ResourcesFolder))
            {
                foreach (var variant in ability.variants)
                {
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
