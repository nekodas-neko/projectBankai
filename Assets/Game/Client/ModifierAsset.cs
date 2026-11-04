using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // A small bonus a dropped weapon can carry. The assets sit in Definitions/Modifiers; to add one, copy an asset, rename it and
    // change the numbers, then list its name in a loot table.
    [CreateAssetMenu(menuName = "Game/Weapon modifier", fileName = "new_modifier")]
    public class ModifierAsset : DefinitionAsset
    {
        public const string Folder = ResourcesFolder + "/Modifiers";

        public string displayName = "";
        [Range(0.8f, 1.3f)] public float powerScale = 1f;
        [Range(0.8f, 1.3f)] public float healthScale = 1f;
        [Range(0.8f, 1.3f)] public float speedScale = 1f;

        public WeaponModifierDef ToDefinition()
        {
            return new WeaponModifierDef { Id = Id, Name = displayName, PowerScale = powerScale, HealthScale = healthScale, SpeedScale = speedScale };
        }
    }

    public static class ModifierCatalog
    {
        // Every modifier in the folder, or an empty registry when any is invalid (the error is logged).
        public static Registry<WeaponModifierDef> Load()
        {
            var modifiers = new Registry<WeaponModifierDef>();
            foreach (var asset in Resources.LoadAll<ModifierAsset>(ModifierAsset.Folder))
            {
                modifiers.Add(asset.ToDefinition());
            }

            var errors = Gear.Validate(modifiers);
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid weapon modifiers, none used:\n" + string.Join("\n", errors));
                return new Registry<WeaponModifierDef>();
            }

            return modifiers;
        }
    }
}
