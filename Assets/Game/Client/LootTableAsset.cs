using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // What a boss can drop. The asset sits in Definitions/Loot; the boss drop table is named boss_drop.
    [CreateAssetMenu(menuName = "Game/Loot table", fileName = "boss_drop")]
    public class LootTableAsset : DefinitionAsset
    {
        public const string Folder = ResourcesFolder + "/Loot";
        public const string BossDrop = "boss_drop";
        public const string MobDrop = "mob_drop";

        public string[] weaponTypes = new string[0];
        [Range(0.8f, 1.3f)] public float powerMin = 0.95f;
        [Range(0.8f, 1.3f)] public float powerMax = 1.15f;

        // A dropped weapon carries one of these (modifier asset names) with this chance.
        public string[] modifiers = new string[0];
        [Range(0f, 1f)] public float modifierChance;

        public LootTableDef ToDefinition()
        {
            return new LootTableDef { Id = Id, WeaponTypes = weaponTypes.ToArray(), PowerMin = powerMin, PowerMax = powerMax, Modifiers = modifiers.ToArray(), ModifierChance = modifierChance };
        }
    }

    public static class LootCatalog
    {
        // Every loot table in the folder, or an empty registry (no drops) when any is invalid; invalid ones are logged.
        public static Registry<LootTableDef> Load(Registry<ClassDef> classes, Registry<WeaponModifierDef> modifiers = null)
        {
            var tables = new Registry<LootTableDef>();
            foreach (var asset in Resources.LoadAll<LootTableAsset>(LootTableAsset.Folder))
            {
                tables.Add(asset.ToDefinition());
            }

            var errors = LootRoller.Validate(tables, classes, modifiers ?? ModifierCatalog.Load());
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid loot tables, no drops:\n" + string.Join("\n", errors));
                return new Registry<LootTableDef>();
            }

            return tables;
        }

        // The boss drop table, or null when it is missing or invalid.
        public static LootTableDef LoadBossDrop(Registry<ClassDef> classes)
        {
            return Load(classes).TryGet(LootTableAsset.BossDrop, out var table) ? table : null;
        }
    }
}
