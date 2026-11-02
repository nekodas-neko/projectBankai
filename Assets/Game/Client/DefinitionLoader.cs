using System;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    public static class DefinitionLoader
    {
        public static void Load(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities)
        {
            Load(out classes, out abilities, out _);
        }

        public static void Load(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities, out Registry<DungeonDef> dungeons)
        {
            dungeons = new Registry<DungeonDef>();
            abilities = new Registry<AbilityDef>();
            classes = new Registry<ClassDef>();

            foreach (var asset in Resources.LoadAll<AbilityAsset>(DefinitionAsset.ResourcesFolder))
            {
                abilities.Add(asset.ToDefinition());
            }

            foreach (var asset in Resources.LoadAll<ClassAsset>(DefinitionAsset.ResourcesFolder))
            {
                classes.Add(asset.ToDefinition());
            }

            foreach (var asset in Resources.LoadAll<DungeonAsset>(DefinitionAsset.ResourcesFolder))
            {
                dungeons.Add(asset.ToDefinition());
            }

            var errors = DefinitionValidator.Validate(classes, abilities);
            errors.AddRange(DefinitionValidator.ValidateDungeons(dungeons, classes));
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Invalid definitions:\n" + string.Join("\n", errors));
            }
        }
    }
}
