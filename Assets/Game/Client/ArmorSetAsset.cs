using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // An armor set a character can wear. The assets sit in Definitions/Armor; to add one, copy an asset, rename it, change the
    // numbers and list its name in a loot table.
    [CreateAssetMenu(menuName = "Game/Armor set", fileName = "new_armor")]
    public class ArmorSetAsset : DefinitionAsset
    {
        public const string Folder = ResourcesFolder + "/Armor";

        public string displayName = "";
        [Range(0.8f, 1.3f)] public float powerScale = 1f;
        [Range(0.8f, 1.3f)] public float healthScale = 1f;
        [Range(0.8f, 1.3f)] public float speedScale = 1f;

        [Tooltip("The outfit art for this set, when there is some.")] public string look = "";

        public ArmorSetDef ToDefinition()
        {
            return new ArmorSetDef { Id = Id, Name = displayName, PowerScale = powerScale, HealthScale = healthScale, SpeedScale = speedScale, Look = look };
        }
    }

    public static class ArmorCatalog
    {
        // Every set in the folder, or an empty registry when any is invalid (the error is logged).
        public static Registry<ArmorSetDef> Load()
        {
            var sets = new Registry<ArmorSetDef>();
            foreach (var asset in Resources.LoadAll<ArmorSetAsset>(ArmorSetAsset.Folder))
            {
                sets.Add(asset.ToDefinition());
            }

            var errors = ArmorRules.Validate(sets);
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid armor sets, none used:\n" + string.Join("\n", errors));
                return new Registry<ArmorSetDef>();
            }

            return sets;
        }
    }
}
