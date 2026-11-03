using System.IO;
using System.Linq;
using Game.Client;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SeedContent
    {
        const string DefinitionsRoot = "Assets/Game/Data/Resources/Definitions";
        const string SkinsRoot = "Assets/Game/Data/Resources/Skins";
        const string MaterialsRoot = "Assets/Game/Art/Materials";
        const string EffectsRoot = "Assets/Game/Art/Effects";

        [MenuItem("Game/Seed everything")]
        public static void SeedAll()
        {
            SeedDefinitions();
            SeedCosmetics();
            ArtImport.ImportAll();
            SeedDungeon();
            SeedArena();
        }

        [MenuItem("Game/Seed default definitions")]
        public static void SeedDefinitions()
        {
            Directory.CreateDirectory(DefinitionsRoot);

            foreach (var obsolete in new[] { "fighter", "training_dummy", "slash", "bolt", "shockwave" })
            {
                AssetDatabase.DeleteAsset($"{DefinitionsRoot}/{obsolete}.asset");
            }

            var swordSlash = Ability("sword_slash", cooldown: 0.6f, range: 2.8f, radius: 1.2f, damage: 9f);
            var shieldBash = Ability("shield_bash", cooldown: 0.5f, range: 3.5f, radius: 1.8f, damage: 14f);
            var holyBolt = Ability("holy_bolt", cooldown: 0.9f, range: 12f, radius: 0.5f, damage: 13f);
            var radiantPulse = Ability("radiant_pulse", cooldown: 0.5f, range: 5f, radius: 3f, damage: 14f);
            var arcaneBolt = Ability("arcane_bolt", cooldown: 0.8f, range: 14f, radius: 0.5f, damage: 8f);
            var fireball = Ability("fireball", cooldown: 0.5f, range: 18f, radius: 1f, damage: 30f);
            var bulwarkCrash = Ability("bulwark_crash", cooldown: 1f, range: 5f, radius: 5f, damage: 40f);
            Tune(bulwarkCrash, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 0.4f; a.taunt = 4f; a.chargeRequired = 1000f; a.chargeFromDealt = 1f; });
            var meteorFall = Ability("meteor_fall", cooldown: 1f, range: 6f, radius: 6f, damage: 70f);
            Tune(meteorFall, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 0.8f; a.chargeRequired = 1000f; a.chargeFromDealt = 0.8f; });
            var radiantBurst = Ability("radiant_burst", cooldown: 1f, range: 6f, radius: 6f, damage: 20f);
            Tune(radiantBurst, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 0.5f; a.heal = 50f; a.chargeRequired = 1000f; a.chargeFromDealt = 1.6f; });
            var slimeSlam = Ability("slime_slam", cooldown: 3.5f, range: 2f, radius: 1.2f, damage: 9f);

            var kingSlam = Ability("king_slam", cooldown: 5f, range: 3.4f, radius: 1.8f, damage: 15f);
            Tune(kingSlam, a => a.windup <= 0f, a => a.windup = 0.8f);
            var kingSmash = Ability("king_smash", cooldown: 6f, range: 4.5f, radius: 4.5f, damage: 16f);
            var slimeBomb = Ability("slime_bomb", cooldown: 0.5f, range: 30f, radius: 3.8f, damage: 14f);
            Tune(slimeBomb, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 0.8f; a.avoidable = false; a.atTarget = true; });
            var kingWave = Ability("king_wave", cooldown: 8f, range: 9f, radius: 0f, damage: 17f);
            Tune(kingSmash, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 1.2f; });
            Tune(kingWave, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Cone; a.angle = 75f; a.windup = 0.9f; });

            var arcaneCounter = Ability("arcane_counter", cooldown: 0f, range: 10f, radius: 2.2f, damage: 26f);
            var arcaneStep = Ability("arcane_step", cooldown: 5.5f, range: 1f, radius: 0f, damage: 0f);
            var shieldWall = Ability("shield_wall", cooldown: 2.5f, range: 1f, radius: 0f, damage: 0f);
            var ward = Ability("ward", cooldown: 6f, range: 1f, radius: 0f, damage: 0f);

            Tune(arcaneStep, a => a.kind == AbilityKind.Attack, a => { a.kind = AbilityKind.Dodge; a.duration = 0.35f; a.dashDistance = 6f; a.perfectWindow = 0.18f; a.rider = arcaneCounter; });
            Tune(shieldWall, a => a.kind == AbilityKind.Attack, a => { a.kind = AbilityKind.Block; a.duration = 1.5f; a.reduction = 0.75f; });
            Tune(ward, a => a.kind == AbilityKind.Attack, a => { a.kind = AbilityKind.Shield; a.duration = 4f; a.absorb = 40f; });
            Tune(slimeSlam, a => a.windup <= 0f, a => a.windup = 0.6f);
            Tune(radiantPulse, a => a.heal <= 0f, a => a.heal = 14f);
            Tune(shieldBash, a => a.taunt <= 0f, a => a.taunt = 3f);
            Tune(shieldBash, a => a.resourceCost == 0f, a => a.resourceCost = 30f);
            Tune(swordSlash, a => a.resourceCost == 0f, a => a.resourceCost = -10f);
            Tune(arcaneBolt, a => a.resourceCost == 0f, a => a.resourceCost = -8f);
            Tune(holyBolt, a => a.resourceCost == 0f, a => a.resourceCost = -6f);
            Tune(radiantPulse, a => a.resourceCost == 0f, a => a.resourceCost = 30f);
            Tune(fireball, a => a.resourceCost == 0f, a => a.resourceCost = 35f);

            Class("sword_and_shield", true, 540f, 4.5f, 1f, swordSlash, shieldBash);
            Class("healing_mace", true, 90f, 6f, 1f, holyBolt, radiantPulse);
            Class("staff", true, 80f, 6.5f, 1.2f, arcaneBolt, fireball);
            Class("slime", false, 60f, 0f, 1f, slimeSlam);
            Class("slime_boss", false, 2400f, 0f, 1f, slimeSlam);
            Class("mushroom_warden", false, 900f, 0f, 1f, slimeSlam);
            Class("brood_mother", false, 1600f, 0f, 1f, kingSlam);

            Tune("sword_and_shield", c => c.defense == null, c =>
            {
                c.defense = shieldWall;
                c.hasResource = true;
                c.resourceId = "rage";
                c.resourceMax = 100f;
                c.resourceStart = 100f;
                c.resourceRegen = 0f;
                c.resourceGainDealt = 0f;
                c.resourceGainTaken = 0.6f;
                c.resourceGainBlocked = 1.2f;
            });
            Tune("healing_mace", c => c.defense == null, c =>
            {
                c.defense = ward;
                c.hasResource = true;
                c.resourceId = "mana";
                c.resourceMax = 100f;
                c.resourceStart = 100f;
                c.resourceRegen = 2f;
                c.resourceGainBlocked = 0.6f;
            });
            Tune("staff", c => c.defense == null, c =>
            {
                c.defense = arcaneStep;
                c.hasResource = true;
                c.resourceId = "focus";
                c.resourceMax = 100f;
                c.resourceStart = 100f;
                c.resourceRegen = 3f;
                c.resourceGainDealt = 0f;
            });
            Tune("sword_and_shield", c => c.skills.Length < 2, c => c.skills = new[] { c.skills[0], bulwarkCrash });
            Tune("healing_mace", c => c.skills.Length < 2, c => c.skills = new[] { c.skills[0], radiantBurst });
            Tune("staff", c => c.skills.Length < 2, c => c.skills = new[] { c.skills[0], meteorFall });
            Tune("sword_and_shield", c => c.threatMultiplier <= 1f, c => c.threatMultiplier = 3f);
            Tune("slime_boss", c => c.skills.Length == 0, c => c.skills = new[] { kingSmash, kingWave });
            Tune("slime_boss", c => c.phases.Length == 0 || c.autoAttack != kingSlam, c =>
            {
                var slime = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/slime.asset");
                var slam = kingSlam;
                c.autoAttack = kingSlam;
                c.phases = new[]
                {
                    new PhaseData { healthBelow = 1f, attacks = new[] { kingSmash, slam } },
                    new PhaseData { healthBelow = 0.65f, attacks = new[] { kingWave, kingSmash, slam }, spawnClass = slime, spawnCount = 2, spawnSkin = "slime_green" },
                    new PhaseData { healthBelow = 0.3f, attacks = new[] { kingSmash, kingWave, slam }, speedScale = 1.5f, spawnClass = slime, spawnCount = 3, spawnSkin = "slime_green" },
                };
            });
            Tune("slime_boss", c => c.skills.Length < 3 && c.phases.Length >= 3 && c.phases[1].mechanics.Length == 0, c =>
            {
                c.skills = new[] { c.skills[0], c.skills[1], slimeBomb };
                c.phases[1].mechanics = new[] { new MechanicData { ability = slimeBomb, intervalSeconds = 7f, firstDelaySeconds = 3f, target = MechanicTarget.Farthest, count = 1 } };
                c.phases[2].mechanics = new[]
                {
                    new MechanicData { ability = slimeBomb, intervalSeconds = 5f, firstDelaySeconds = 2f, target = MechanicTarget.Farthest, count = 1 },
                    new MechanicData { ability = slimeBomb, intervalSeconds = 8f, firstDelaySeconds = 4f, target = MechanicTarget.Random, count = 1 },
                };
            });
            Tune("brood_mother", c => c.skills.Length < 2 && c.phases.Length >= 2 && c.phases[1].mechanics.Length == 0, c =>
            {
                c.skills = new[] { c.skills[0], slimeBomb };
                c.phases[1].mechanics = new[] { new MechanicData { ability = slimeBomb, intervalSeconds = 8f, firstDelaySeconds = 3f, target = MechanicTarget.NotTopThreat, count = 1 } };
            });
            Tune("mushroom_warden", c => c.skills.Length < 2 && c.phases.Length >= 1 && c.phases[0].mechanics.Length == 0, c =>
            {
                c.skills = new[] { c.skills[0], slimeBomb };
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeBomb, intervalSeconds = 4.5f, firstDelaySeconds = 5f, target = MechanicTarget.Farthest, count = 1 } };
            });
            Tune("brood_mother", c => c.phases.Length >= 1 && c.phases[0].mechanics.Length == 0, c =>
            {
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeBomb, intervalSeconds = 7f, firstDelaySeconds = 5f, target = MechanicTarget.Farthest, count = 1 } };
            });
            Tune("slime_boss", c => c.phases.Length >= 1 && c.phases[0].mechanics.Length == 0, c =>
            {
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeBomb, intervalSeconds = 10f, firstDelaySeconds = 6f, target = MechanicTarget.Farthest, count = 1 } };
            });
            // Pacing: ground circles on the early bosses are orange (the weapon's own defence answers them), and red attacks are rare.
            var slimeSpit = Ability("slime_spit", cooldown: 0.5f, range: 30f, radius: 4.6f, damage: 18f);
            Tune(slimeSpit, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 1.2f; a.avoidable = true; a.atTarget = true; });
            Tune(kingSmash, a => a.cooldown < 10f, a => a.cooldown = 14f);
            Tune(kingSmash, a => !a.avoidable, a => a.avoidable = true);
            Tune("mushroom_warden", c => c.phases[0].mechanics.Length > 0 && c.phases[0].mechanics[0].ability == slimeBomb, c =>
            {
                c.skills = new[] { c.skills[0], slimeSpit };
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeSpit, intervalSeconds = 8f, firstDelaySeconds = 4f, target = MechanicTarget.Farthest, count = 1 } };
            });
            Tune("brood_mother", c => c.phases[0].mechanics.Length > 0 && c.phases[0].mechanics[0].ability == slimeBomb, c =>
            {
                c.skills = new[] { c.skills[0], slimeBomb, slimeSpit };
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeSpit, intervalSeconds = 10f, firstDelaySeconds = 5f, target = MechanicTarget.Farthest, count = 1 } };
                c.phases[1].mechanics[0].intervalSeconds = 18f;
            });
            Tune("slime_boss", c => c.phases[0].mechanics.Length > 0 && c.phases[0].mechanics[0].ability == slimeBomb, c =>
            {
                c.skills = new[] { c.skills[0], c.skills[1], slimeSpit };
                c.phases[0].mechanics = new[] { new MechanicData { ability = slimeSpit, intervalSeconds = 14f, firstDelaySeconds = 6f, target = MechanicTarget.Farthest, count = 1 } };
                c.phases[1].mechanics = new[] { new MechanicData { ability = slimeSpit, intervalSeconds = 12f, firstDelaySeconds = 3f, target = MechanicTarget.Farthest, count = 1 } };
                c.phases[2].mechanics = new[]
                {
                    new MechanicData { ability = slimeSpit, intervalSeconds = 10f, firstDelaySeconds = 2f, target = MechanicTarget.Farthest, count = 1 },
                    new MechanicData { ability = slimeSpit, intervalSeconds = 16f, firstDelaySeconds = 4f, target = MechanicTarget.Random, count = 1 },
                };
            });
            Tune("mushroom_warden", c => c.recoverySeconds <= 0f, c => c.recoverySeconds = 1f);
            Tune("brood_mother", c => c.recoverySeconds <= 0f, c => c.recoverySeconds = 1.3f);
            Tune("slime_boss", c => c.recoverySeconds <= 0f, c => c.recoverySeconds = 1.5f);
            // Casts: bosses stand still while they wind these up, and shield bash and fireball can interrupt them.
            var slimeClass = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/slime.asset");
            var devour = Ability("devour", cooldown: 0.5f, range: 6f, radius: 0.01f, damage: 0f);
            Tune(devour, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 2.2f; a.heal = 140f; a.interruptible = true; });
            var broodCall = Ability("brood_call", cooldown: 0.5f, range: 6f, radius: 0.01f, damage: 0f);
            Tune(broodCall, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Circle; a.windup = 2.5f; a.interruptible = true; a.summonClass = slimeClass; a.summonSkin = "slime_green"; a.summonCount = 3; });
            Tune(shieldBash, a => a.interrupt <= 0f, a => a.interrupt = 2f);
            Tune(fireball, a => a.interrupt <= 0f, a => a.interrupt = 1.5f);
            Tune("mushroom_warden", c => c.skills.Length < 3 && !c.skills.Contains(devour), c =>
            {
                c.skills = c.skills.Concat(new[] { devour }).ToArray();
                c.phases[0].mechanics = c.phases[0].mechanics.Concat(new[] { new MechanicData { ability = devour, intervalSeconds = 18f, firstDelaySeconds = 10f, target = MechanicTarget.Random, count = 1 } }).ToArray();
            });
            Tune("brood_mother", c => c.skills.Contains(slimeSpit) && !c.skills.Contains(broodCall), c =>
            {
                c.skills = new[] { c.skills[0], slimeBomb, broodCall };
                c.phases[0].mechanics = new[] { new MechanicData { ability = broodCall, intervalSeconds = 22f, firstDelaySeconds = 12f, target = MechanicTarget.Random, count = 1 } };
                c.phases[1].mechanics = new[] { c.phases[1].mechanics[0], new MechanicData { ability = broodCall, intervalSeconds = 22f, firstDelaySeconds = 14f, target = MechanicTarget.Random, count = 1 } };
            });
            // The king's own cast: a long charge-up cone. Catch it with a shield bash or a fireball and he is staggered; otherwise block or leave the cone.
            var kingCleave = Ability("king_cleave", cooldown: 15f, range: 9f, radius: 0f, damage: 24f);
            Tune(kingCleave, a => a.shape == AbilityShape.Line, a => { a.shape = AbilityShape.Cone; a.angle = 75f; a.windup = 2f; a.interruptible = true; });
            Tune("slime_boss", c => c.skills.Contains(kingWave) && !c.skills.Contains(kingCleave), c =>
            {
                c.skills = c.skills.Select(skill => skill == kingWave ? kingCleave : skill).ToArray();
                foreach (var phase in c.phases)
                {
                    phase.attacks = phase.attacks.Select(attack => attack == kingWave ? kingCleave : attack).ToArray();
                }
            });
            Tune("sword_and_shield", c => c.bodyRadius <= 0f, c => c.bodyRadius = 0.5f);
            Tune("healing_mace", c => c.bodyRadius <= 0f, c => c.bodyRadius = 0.5f);
            Tune("staff", c => c.bodyRadius <= 0f, c => c.bodyRadius = 0.5f);
            Tune("sword_and_shield", c => string.IsNullOrEmpty(c.displayName), c => { c.displayName = "Sword and shield"; c.skinPrefix = "tank"; });
            Tune("healing_mace", c => string.IsNullOrEmpty(c.displayName), c => { c.displayName = "Healing mace"; c.skinPrefix = "healer"; });
            Tune("staff", c => string.IsNullOrEmpty(c.displayName), c => { c.displayName = "Staff"; c.skinPrefix = "mage"; });
            Tune("slime", c => c.bodyRadius <= 0f, c => c.bodyRadius = 0.7f);
            Tune("slime_boss", c => c.bodyRadius <= 0f, c => c.bodyRadius = 1.8f);
            Tune("slime", c => c.aggroRange <= 0f, c => { c.aggroRange = 12f; c.moveSpeed = 2.8f; });
            Tune("slime_boss", c => c.aggroRange <= 0f, c => { c.aggroRange = 22f; c.moveSpeed = 2.2f; });
            Tune("slime", c => c.leashRange <= 0f, c => { c.aggroRange = 9f; c.leashRange = 18f; });
            Tune("slime_boss", c => c.aggroRange > 12f, c => c.aggroRange = 12f);
            Tune("mushroom_warden", c => c.aggroRange <= 0f, c =>
            {
                c.aggroRange = 10f;
                c.moveSpeed = 2.6f;
                c.bodyRadius = 1.2f;
                c.skills = new[] { kingSmash };
                c.phases = new[] { new PhaseData { healthBelow = 1f, attacks = new[] { kingSmash, slimeSlam } } };
            });
            Tune("brood_mother", c => c.aggroRange <= 0f, c =>
            {
                var slime = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/slime.asset");
                c.aggroRange = 11f;
                c.moveSpeed = 2.4f;
                c.bodyRadius = 1.5f;
                c.skills = new[] { kingWave };
                c.phases = new[]
                {
                    new PhaseData { healthBelow = 1f, attacks = new[] { kingWave, kingSlam } },
                    new PhaseData { healthBelow = 0.5f, attacks = new[] { kingWave, kingSlam }, speedScale = 1.3f, spawnClass = slime, spawnCount = 2, spawnSkin = "slime_green" },
                };
            });

            // Boss loot: a personal drop of one of the three weapons, a little stronger or weaker than the starter one.
            var lootFolder = $"Assets/Game/Data/Resources/{LootTableAsset.Folder}";
            Directory.CreateDirectory(lootFolder);
            var lootPath = $"{lootFolder}/{LootTableAsset.BossDrop}.asset";
            if (AssetDatabase.LoadAssetAtPath<LootTableAsset>(lootPath) == null)
            {
                var table = ScriptableObject.CreateInstance<LootTableAsset>();
                table.weaponTypes = new[] { "sword_and_shield", "staff", "healing_mace" };
                table.powerMin = 0.95f;
                table.powerMax = 1.15f;
                AssetDatabase.CreateAsset(table, lootPath);
            }

            // Mob drops: a rare, slightly weaker weapon. The same shape as the boss table, so a new table is a copy with other numbers.
            var mobPath = $"{lootFolder}/{LootTableAsset.MobDrop}.asset";
            if (AssetDatabase.LoadAssetAtPath<LootTableAsset>(mobPath) == null)
            {
                var table = ScriptableObject.CreateInstance<LootTableAsset>();
                table.weaponTypes = new[] { "sword_and_shield", "staff", "healing_mace" };
                table.powerMin = 0.9f;
                table.powerMax = 1.05f;
                AssetDatabase.CreateAsset(table, mobPath);
            }

            // What kills are worth: XP for the weapon in hand and a drop chance per player. Bosses always drop from the boss table.
            Tune("slime", c => string.IsNullOrEmpty(c.lootTable), c => { c.xpReward = 10; c.lootTable = LootTableAsset.MobDrop; c.dropChance = 0.03f; });
            foreach (var boss in new[] { "slime_boss", "mushroom_warden", "brood_mother" })
            {
                Tune(boss, c => string.IsNullOrEmpty(c.lootTable), c => { c.xpReward = 150; c.lootTable = LootTableAsset.BossDrop; c.dropChance = 1f; });
            }

            // Skill trees: each node picks a variant for one slot (0 attack, 1 skill, 2 ultimate, 4 defence).
            Tree("sword_and_shield",
                Node("ember_slash", "Ember slash", 0, "ember"),
                Node("heavy_bash", "Heavy bash", 1, "heavy"),
                Node("crushing_crash", "Crushing crash", 2, "crushing", 2, "heavy_bash"),
                Node("quick_wall", "Quick wall", 4, "quick"));
            Tree("staff",
                Node("crimson_bolt", "Crimson bolt", 0, "crimson"),
                Node("frost_fireball", "Frost fireball", 1, "frost"),
                Node("wide_meteor", "Wide meteor", 2, "wide", 2, "frost_fireball"),
                Node("quick_step", "Quick step", 4, "quick"));
            Tree("healing_mace",
                Node("verdant_bolt", "Verdant bolt", 0, "verdant"),
                Node("quick_pulse", "Quick pulse", 1, "quick"),
                Node("greater_burst", "Greater burst", 2, "greater", 2, "quick_pulse"),
                Node("quick_ward", "Quick ward", 4, "quick"));

            // Mastery: 20 levels per weapon type, one point each, filled by kills with that type in hand.
            var masteryPath = $"{DefinitionsRoot}/{MasteryCatalog.AssetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<MasteryAsset>(masteryPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MasteryAsset>(), masteryPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/Seed placeholder skins and effects")]
        public static void SeedCosmetics()
        {
            Directory.CreateDirectory(SkinsRoot);
            Directory.CreateDirectory($"{MaterialsRoot}/Skins");
            Directory.CreateDirectory(EffectsRoot);

            Skin("default", new Color(0.22f, 0.54f, 0.87f), new Color(0.7f, 0.85f, 1f), new Vector3(1f, 1f, 1f), false);
            Skin("tank_steel", new Color(0.55f, 0.6f, 0.68f), new Color(0.95f, 0.8f, 0.3f), new Vector3(1.15f, 1.05f, 1.15f), true);
            Skin("tank_gold", new Color(0.9f, 0.72f, 0.2f), new Color(0.55f, 0.6f, 0.68f), new Vector3(1.15f, 1.05f, 1.15f), true);
            Skin("healer_light", new Color(0.93f, 0.92f, 0.85f), new Color(1f, 0.85f, 0.3f), new Vector3(0.95f, 1f, 0.95f), true);
            Skin("healer_verdant", new Color(0.2f, 0.62f, 0.55f), new Color(0.93f, 0.92f, 0.85f), new Vector3(0.95f, 1f, 0.95f), true);
            Skin("mage_arcane", new Color(0.45f, 0.3f, 0.8f), new Color(0.4f, 0.85f, 1f), new Vector3(0.9f, 1.05f, 0.9f), true);
            Skin("mage_crimson", new Color(0.7f, 0.15f, 0.3f), new Color(1f, 0.8f, 0.3f), new Vector3(0.9f, 1.05f, 0.9f), true);
            Skin("slime_green", new Color(0.35f, 0.8f, 0.5f), new Color(0.8f, 1f, 0.85f), new Vector3(1.4f, 1.1f, 1.4f), false, SkinAsset.BodyShape.Sphere, 0.25f);
            Skin("slime_king", new Color(0.3f, 0.75f, 0.6f), new Color(0.95f, 0.8f, 0.25f), new Vector3(3.2f, 2.6f, 3.2f), false, SkinAsset.BodyShape.Sphere, 0.4f);

            var cube = PrimitiveMesh(PrimitiveType.Cube);
            var slash = Burst("fx_cast_slash", new Color(1f, 0.95f, 0.75f), 18, 9f, 0.16f, 0.25f, 35f, cube);
            var shield = Burst("fx_cast_shield", new Color(0.95f, 0.8f, 0.3f), 22, 7f, 0.22f, 0.3f, 45f, cube);
            var holy = Burst("fx_cast_holy", new Color(1f, 0.95f, 0.5f), 20, 8f, 0.16f, 0.35f, 25f, cube);
            var pulse = Burst("fx_cast_pulse", new Color(1f, 0.95f, 0.6f), 40, 6f, 0.18f, 0.5f, 0f, cube);
            var arcane = Burst("fx_cast_arcane", new Color(0.55f, 0.75f, 1f), 24, 11f, 0.14f, 0.35f, 20f, cube);
            var fire = Burst("fx_cast_fire", new Color(1f, 0.55f, 0.15f), 30, 12f, 0.2f, 0.4f, 25f, cube);
            var slime = Burst("fx_cast_slime", new Color(0.4f, 0.9f, 0.55f), 14, 5f, 0.18f, 0.3f, 40f, cube);
            var hitSpark = Burst("fx_hit_spark", new Color(1f, 0.95f, 0.7f), 14, 6f, 0.14f, 0.3f, 0f, cube);
            var hitHoly = Burst("fx_hit_holy", new Color(1f, 0.95f, 0.5f), 16, 5f, 0.14f, 0.4f, 0f, cube);
            var hitArcane = Burst("fx_hit_arcane", new Color(0.6f, 0.75f, 1f), 16, 6f, 0.14f, 0.35f, 0f, cube);
            var hitFire = Burst("fx_hit_fire", new Color(1f, 0.5f, 0.1f), 28, 7f, 0.2f, 0.45f, 0f, cube);
            var hitSlime = Burst("fx_hit_slime", new Color(0.4f, 0.9f, 0.55f), 12, 4f, 0.16f, 0.35f, 0f, cube);

            Link("sword_slash", slash, hitSpark);
            Link("shield_bash", shield, hitSpark);
            Link("holy_bolt", holy, hitHoly);
            Link("radiant_pulse", pulse, hitHoly);
            Link("arcane_bolt", arcane, hitArcane);
            Link("fireball", fire, hitFire);
            Link("slime_slam", slime, hitSlime);
            Link("king_slam", slime, hitSlime);
            Link("king_smash", slime, hitSlime);
            Link("king_wave", slime, hitSlime);
            Link("king_cleave", slime, hitSlime);
            Link("slime_bomb", slime, hitSlime);
            Link("slime_spit", slime, hitSlime);
            Link("devour", holy, hitHoly);
            Link("brood_call", slime, hitSlime);
            Link("bulwark_crash", shield, hitSpark);
            Link("meteor_fall", fire, hitFire);
            Link("radiant_burst", pulse, hitHoly);
            var crimsonCast = Burst("fx_cast_crimson", new Color(0.95f, 0.2f, 0.3f), 24, 11f, 0.14f, 0.35f, 20f, cube);
            var crimsonHit = Burst("fx_hit_crimson", new Color(1f, 0.3f, 0.35f), 16, 6f, 0.14f, 0.35f, 0f, cube);
            var frostCast = Burst("fx_cast_frost", new Color(0.55f, 0.9f, 1f), 30, 12f, 0.2f, 0.4f, 25f, cube);
            var frostHit = Burst("fx_hit_frost", new Color(0.7f, 0.95f, 1f), 28, 7f, 0.2f, 0.45f, 0f, cube);
            var verdantCast = Burst("fx_cast_verdant", new Color(0.4f, 0.95f, 0.5f), 20, 8f, 0.16f, 0.35f, 25f, cube);
            var verdantHit = Burst("fx_hit_verdant", new Color(0.5f, 1f, 0.55f), 16, 5f, 0.14f, 0.4f, 0f, cube);
            var emberCast = Burst("fx_cast_ember", new Color(1f, 0.5f, 0.1f), 18, 9f, 0.16f, 0.25f, 35f, cube);
            var emberHit = Burst("fx_hit_ember", new Color(1f, 0.6f, 0.15f), 14, 6f, 0.14f, 0.3f, 0f, cube);
            Variant("arcane_bolt", "crimson", 1.15f, 1f, 1.1f, crimsonCast, crimsonHit);
            Variant("fireball", "frost", 0.9f, 1.25f, 0.9f, frostCast, frostHit);
            Variant("holy_bolt", "verdant", 1.1f, 1f, 1f, verdantCast, verdantHit);
            Variant("sword_slash", "ember", 1.2f, 1f, 1.15f, emberCast, emberHit);
            // Skill tree choices that only change numbers (they borrow the base skill's effects).
            Variant("shield_bash", "heavy", 1.3f, 1f, 1.15f, null, null);
            Variant("bulwark_crash", "crushing", 1.25f, 1f, 1.2f, null, null);
            Variant("shield_wall", "quick", 1f, 1f, 0.75f, null, null);
            Variant("meteor_fall", "wide", 0.9f, 1.25f, 1f, null, null);
            Variant("arcane_step", "quick", 1f, 1f, 0.75f, null, null);
            Variant("radiant_pulse", "quick", 0.9f, 1f, 0.8f, null, null);
            Variant("radiant_burst", "greater", 1.3f, 1f, 1.15f, null, null);
            Variant("ward", "quick", 1f, 1f, 0.75f, null, null);
            Link("arcane_step", arcane, hitSpark);
            Link("arcane_counter", fire, hitArcane);
            Link("shield_wall", shield, hitSpark);
            Link("ward", holy, hitHoly);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/Seed slime cave dungeon")]
        public static void SeedDungeon()
        {
            var path = $"{DefinitionsRoot}/slime_cave.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DungeonAsset>(path);
            if (asset != null && asset.zones.Length >= 14 && asset.rooms.Any(room => room.spawns.Any(spawn => spawn.boss)))
            {
                return;
            }

            var slime = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/slime.asset");
            var warden = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/mushroom_warden.asset");
            var mother = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/brood_mother.asset");
            var king = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/slime_boss.asset");
            var created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<DungeonAsset>();
            }

            asset.clearDelaySeconds = 3f;
            asset.healthPerExtraPlayer = 0.8f;
            // A winding cave: north out of the entry hall to the warden, east and north through the brood tunnels, then west and north
            // to the king. Side caves hold optional packs; every section ends at its boss and the next gate.
            asset.rooms = new[]
            {
                new RoomData
                {
                    name = "Mushroom grove",
                    spawns = new[]
                    {
                        Spawn(slime, -21f, 5f, "slime_green", pack: 1),
                        Spawn(slime, -18f, 8f, "slime_green", pack: 1),
                        Spawn(slime, -22f, 10f, "slime_green", pack: 1),
                        Spawn(slime, 19f, 5f, "slime_green", pack: 2),
                        Spawn(slime, 22f, 9f, "slime_green", pack: 2),
                        Spawn(slime, 17f, 10f, "slime_green", pack: 2),
                        Spawn(slime, -3f, 19f, "slime_green", pack: 3),
                        Spawn(slime, 3f, 21f, "slime_green", pack: 3),
                        Spawn(warden, 0f, 31f, "monster_mushroom", boss: true),
                    },
                },
                new RoomData
                {
                    name = "Brood tunnels",
                    spawns = new[]
                    {
                        Spawn(slime, 22f, 27f, "slime_green", pack: 1),
                        Spawn(slime, 25f, 30f, "slime_green", pack: 1),
                        Spawn(slime, 23f, 31f, "slime_green", pack: 1),
                        Spawn(slime, 37f, 36f, "slime_green", pack: 2),
                        Spawn(slime, 39f, 40f, "slime_green", pack: 2),
                        Spawn(slime, 36f, 43f, "slime_green", pack: 2),
                        Spawn(slime, 24f, 44f, "slime_green", pack: 3),
                        Spawn(slime, 27f, 46f, "slime_green", pack: 3),
                        Spawn(slime, 23f, 47f, "slime_green", pack: 3),
                        Spawn(slime, 18f, 66f, "slime_green", pack: 4),
                        Spawn(slime, 30f, 66f, "slime_green", pack: 4),
                        Spawn(mother, 25f, 73f, "monster_cthulhu", boss: true),
                    },
                },
                new RoomData
                {
                    name = "King's road",
                    spawns = new[]
                    {
                        Spawn(slime, -4f, 66f, "slime_green", pack: 1),
                        Spawn(slime, -9f, 68f, "slime_green", pack: 1),
                        Spawn(slime, -13f, 65f, "slime_green", pack: 1),
                        Spawn(slime, -19f, 79f, "slime_green", pack: 2),
                        Spawn(slime, -17f, 83f, "slime_green", pack: 2),
                        Spawn(slime, -21f, 88f, "slime_green", pack: 3),
                        Spawn(slime, -17f, 90f, "slime_green", pack: 3),
                        Spawn(slime, -24f, 104f, "slime_green", pack: 4),
                        Spawn(slime, -10f, 104f, "slime_green", pack: 4),
                        Spawn(king, -18f, 114f, "boss_greendemon", boss: true),
                    },
                },
            };
            asset.zones = new[]
            {
                new ZoneData { name = "Entry hall", min = new Vector2(-8f, -8f), max = new Vector2(8f, 10f) },
                new ZoneData { name = "West passage", min = new Vector2(-12f, 3f), max = new Vector2(-6f, 7f) },
                new ZoneData { name = "West cave", min = new Vector2(-28f, -2f), max = new Vector2(-10f, 12f) },
                new ZoneData { name = "East passage", min = new Vector2(6f, 3f), max = new Vector2(12f, 7f) },
                new ZoneData { name = "East cave", min = new Vector2(10f, -2f), max = new Vector2(28f, 12f) },
                new ZoneData { name = "Grove path", min = new Vector2(-3f, 8f), max = new Vector2(3f, 16f) },
                new ZoneData { name = "Warden's chamber", min = new Vector2(-12f, 14f), max = new Vector2(12f, 38f) },
                new ZoneData { name = "Brood tunnel east", min = new Vector2(14f, 24f), max = new Vector2(40f, 34f) },
                new ZoneData { name = "Brood tunnel north", min = new Vector2(32f, 30f), max = new Vector2(42f, 60f) },
                new ZoneData { name = "Side nook", min = new Vector2(20f, 40f), max = new Vector2(33f, 50f) },
                new ZoneData { name = "Brood chamber", min = new Vector2(8f, 56f), max = new Vector2(42f, 80f) },
                new ZoneData { name = "Cold passage", min = new Vector2(-24f, 60f), max = new Vector2(6f, 72f) },
                new ZoneData { name = "Hall approach", min = new Vector2(-24f, 70f), max = new Vector2(-14f, 94f) },
                new ZoneData { name = "King's hall", min = new Vector2(-32f, 92f), max = new Vector2(-4f, 124f) },
            };
            asset.gates = new[]
            {
                new GateData { name = "First gate", min = new Vector2(10f, 26f), max = new Vector2(18f, 32f), openAfter = 0 },
                new GateData { name = "Second gate", min = new Vector2(2f, 62f), max = new Vector2(10f, 70f), openAfter = 1 },
            };
            if (created)
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
        }

        static SpawnData Spawn(ClassAsset spawnClass, float x, float y, string skin, bool boss = false, int pack = 0)
        {
            return new SpawnData { spawnClass = spawnClass, position = new Vector2(x, y), skin = skin, boss = boss, pack = pack };
        }

        [MenuItem("Game/Seed arena scene")]
        public static void SeedArena()
        {
            const string scenePath = "Assets/Game/Scenes/Arena.unity";
            Directory.CreateDirectory(MaterialsRoot);

            var assets = new ArenaAssets
            {
                capsule = PrimitiveMesh(PrimitiveType.Capsule),
                sphere = PrimitiveMesh(PrimitiveType.Sphere),
                cube = PrimitiveMesh(PrimitiveType.Cube),
                cylinder = PrimitiveMesh(PrimitiveType.Cylinder),
                player = SeedMaterial($"{MaterialsRoot}/Player.mat", new Color(0.22f, 0.54f, 0.87f)),
                ally = SeedMaterial($"{MaterialsRoot}/Ally.mat", new Color(0.50f, 0.47f, 0.87f)),
                enemy = SeedMaterial($"{MaterialsRoot}/Enemy.mat", new Color(0.94f, 0.62f, 0.15f)),
                dead = SeedMaterial($"{MaterialsRoot}/Dead.mat", new Color(0.35f, 0.35f, 0.35f)),
                flash = SeedMaterial($"{MaterialsRoot}/Flash.mat", Color.white),
                barBackground = SeedMaterial($"{MaterialsRoot}/BarBackground.mat", new Color(0.1f, 0.1f, 0.12f)),
                aim = SeedMaterial($"{MaterialsRoot}/Aim.mat", new Color(0.78f, 0.88f, 1f)),
                telegraph = SeedMaterial($"{MaterialsRoot}/Telegraph.mat", new Color(0.55f, 0.2f, 0.62f), "Universal Render Pipeline/Unlit"),
                telegraphFill = SeedMaterial($"{MaterialsRoot}/TelegraphFill.mat", new Color(1f, 0.95f, 0.35f), "Universal Render Pipeline/Unlit"),
            };
            SetColor(assets.telegraph, new Color(0.55f, 0.2f, 0.62f));
            SetColor(assets.telegraphFill, new Color(1f, 0.95f, 0.35f));
            var ground = SeedMaterial($"{MaterialsRoot}/Ground.mat", new Color(0.23f, 0.25f, 0.29f));
            var rim = SeedMaterial($"{MaterialsRoot}/Rim.mat", new Color(0.55f, 0.58f, 0.64f));

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.065f, 0.08f);
            camera.fieldOfView = 45f;
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 26f, -12f), Quaternion.Euler(60f, 0f, 0f));

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var rimObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rimObject.name = "Rim";
            Object.DestroyImmediate(rimObject.GetComponent<Collider>());
            rimObject.transform.localScale = new Vector3(41.2f, 0.05f, 41.2f);
            rimObject.transform.position = new Vector3(0f, -0.06f, 0f);
            rimObject.GetComponent<Renderer>().sharedMaterial = rim;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Ground";
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.localScale = new Vector3(40f, 0.05f, 40f);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.GetComponent<Renderer>().sharedMaterial = ground;

            SetColor(ground, new Color(0.30f, 0.44f, 0.24f));
            SetColor(rim, new Color(0.42f, 0.34f, 0.24f));
            ArtImport.PlaceProps(null);

            var bootstrapObject = new GameObject("ArenaBootstrap");
            bootstrapObject.AddComponent<ArenaBootstrap>().assets = assets;

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static void SetColor(Material material, Color color)
        {
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        static Material SeedMaterial(string path, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(shader));
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.2f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Skin(string id, Color body, Color accent, Vector3 scale, bool playable, SkinAsset.BodyShape shape = SkinAsset.BodyShape.Capsule, float bob = 0.14f)
        {
            var path = $"{SkinsRoot}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkinAsset>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<SkinAsset>();
            asset.playable = playable;
            asset.shape = shape;
            asset.bobHeight = bob;
            asset.scale = scale;
            asset.body = SeedMaterial($"{MaterialsRoot}/Skins/{id}_body.mat", body);
            asset.accent = SeedMaterial($"{MaterialsRoot}/Skins/{id}_accent.mat", accent);
            AssetDatabase.CreateAsset(asset, path);
        }

        static GameObject Burst(string id, Color color, int count, float speed, float size, float life, float coneAngle, Mesh mesh)
        {
            var path = $"{EffectsRoot}/{id}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(id);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size * 2.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = coneAngle > 0f ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
            shape.angle = coneAngle;
            shape.radius = 0.15f;

            var sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = SeedMaterial($"{MaterialsRoot}/Fx_{id}.mat", color, "Universal Render Pipeline/Unlit");

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void Variant(string abilityId, string id, float damageScale, float rangeScale, float cooldownScale, GameObject cast, GameObject hit)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>($"{DefinitionsRoot}/{abilityId}.asset");
            if (asset == null || System.Array.Exists(asset.variants, v => v.id == id))
            {
                return;
            }

            var list = new System.Collections.Generic.List<VariantData>(asset.variants)
            {
                new VariantData { id = id, damageScale = damageScale, rangeScale = rangeScale, cooldownScale = cooldownScale, castEffect = cast, hitEffect = hit },
            };
            asset.variants = list.ToArray();
            EditorUtility.SetDirty(asset);
        }

        static void Link(string abilityId, GameObject cast, GameObject hit)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>($"{DefinitionsRoot}/{abilityId}.asset");
            if (asset == null)
            {
                return;
            }

            if (asset.castEffect == null)
            {
                asset.castEffect = cast;
            }

            if (asset.hitEffect == null)
            {
                asset.hitEffect = hit;
            }

            EditorUtility.SetDirty(asset);
        }

        static SkillNodeData Node(string id, string displayName, int slot, string variant, int cost = 1, string requires = "")
        {
            return new SkillNodeData { id = id, displayName = displayName, slot = slot, variant = variant, cost = cost, requires = requires };
        }

        static void Tree(string weapon, params SkillNodeData[] nodes)
        {
            var folder = $"Assets/Game/Data/Resources/{SkillTreeAsset.Folder}";
            Directory.CreateDirectory(folder);
            var path = $"{folder}/{weapon}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<SkillTreeAsset>();
            asset.nodes = nodes;
            AssetDatabase.CreateAsset(asset, path);
        }

        static AbilityAsset Ability(string id, float cooldown, float range, float radius, float damage)
        {
            var path = $"{DefinitionsRoot}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<AbilityAsset>();
            asset.cooldown = cooldown;
            asset.range = range;
            asset.radius = radius;
            asset.damage = damage;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void Tune(AbilityAsset asset, System.Func<AbilityAsset, bool> needed, System.Action<AbilityAsset> apply)
        {
            if (asset == null || !needed(asset))
            {
                return;
            }

            apply(asset);
            EditorUtility.SetDirty(asset);
        }

        static void Tune(string classId, System.Func<ClassAsset, bool> needed, System.Action<ClassAsset> apply)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ClassAsset>($"{DefinitionsRoot}/{classId}.asset");
            if (asset == null || !needed(asset))
            {
                return;
            }

            apply(asset);
            EditorUtility.SetDirty(asset);
        }

        static void Class(string id, bool playable, float health, float speed, float power, AbilityAsset auto, params AbilityAsset[] skills)
        {
            var path = $"{DefinitionsRoot}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ClassAsset>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<ClassAsset>();
            asset.playable = playable;
            asset.maxHealth = health;
            asset.moveSpeed = speed;
            asset.power = power;
            asset.autoAttack = auto;
            asset.skills = skills;
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
