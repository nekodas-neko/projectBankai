using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [Serializable]
    public class SkillNodeData
    {
        public string id = "";
        public string displayName = "";

        [Tooltip("0 auto attack, 1 skill, 2 ultimate, 4 defence")]
        [Min(0)] public int slot;

        [Tooltip("The variant id on that slot's ability")]
        public string variant = "";
        [Min(0)] public int cost = 1;

        [Tooltip("Another node of this tree that must be taken first")]
        public string requires = "";
    }

    // One weapon type's skill tree. The asset is named after the weapon type, in Definitions/Trees.
    [CreateAssetMenu(menuName = "Game/Skill tree", fileName = "new_weapon")]
    public class SkillTreeAsset : DefinitionAsset
    {
        public const string Folder = ResourcesFolder + "/Trees";

        public SkillNodeData[] nodes = new SkillNodeData[0];

        public SkillTreeDef ToDefinition()
        {
            return new SkillTreeDef
            {
                Id = Id,
                Nodes = nodes.Select(node => new SkillNodeDef
                {
                    Id = node.id,
                    Name = node.displayName,
                    Slot = node.slot,
                    Variant = node.variant,
                    Cost = node.cost,
                    Requires = node.requires ?? string.Empty,
                }).ToArray(),
            };
        }
    }

    public static class SkillTreeCatalog
    {
        // Every shipped tree. If any is invalid they are all ignored (the default kits stay) and the problem is logged.
        public static Registry<SkillTreeDef> Load(Registry<ClassDef> classes, Registry<AbilityDef> abilities)
        {
            var trees = new Registry<SkillTreeDef>();
            foreach (var asset in Resources.LoadAll<SkillTreeAsset>(SkillTreeAsset.Folder))
            {
                trees.Add(asset.ToDefinition());
            }

            var errors = SkillTreeValidator.Validate(trees, classes, abilities);
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid skill trees, ignoring them:\n" + string.Join("\n", errors));
                return new Registry<SkillTreeDef>();
            }

            return trees;
        }

        public static List<string> Errors(Registry<SkillTreeDef> trees, Registry<ClassDef> classes, Registry<AbilityDef> abilities)
        {
            return SkillTreeValidator.Validate(trees, classes, abilities);
        }
    }
}
