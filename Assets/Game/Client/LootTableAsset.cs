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

        public string[] weaponTypes = new string[0];
        [Range(0.8f, 1.3f)] public float powerMin = 0.95f;
        [Range(0.8f, 1.3f)] public float powerMax = 1.15f;

        public LootTableDef ToDefinition()
        {
            return new LootTableDef { Id = Id, WeaponTypes = weaponTypes.ToArray(), PowerMin = powerMin, PowerMax = powerMax };
        }
    }

    public static class LootCatalog
    {
        // The boss drop table, or null (no drops) when it is missing or invalid; an invalid one is logged.
        public static LootTableDef LoadBossDrop(Registry<ClassDef> classes)
        {
            var tables = new Registry<LootTableDef>();
            foreach (var asset in Resources.LoadAll<LootTableAsset>(LootTableAsset.Folder))
            {
                tables.Add(asset.ToDefinition());
            }

            var errors = LootRoller.Validate(tables, classes);
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid loot tables, no drops:\n" + string.Join("\n", errors));
                return null;
            }

            return tables.TryGet(LootTableAsset.BossDrop, out var table) ? table : null;
        }
    }
}
