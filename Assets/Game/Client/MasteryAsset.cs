using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The mastery rules as data: how much XP each level costs, how many points a level gives, and what a kill is worth.
    [CreateAssetMenu(menuName = "Game/Mastery rules", fileName = "mastery")]
    public class MasteryAsset : DefinitionAsset
    {
        [Min(1)] public int[] xpPerLevel = MasteryRules.DefaultXpPerLevel();
        [Min(0)] public int pointsPerLevel = 1;
        [Min(0)] public int xpPerKill = 10;
        [Min(0)] public int xpPerBossKill = 150;

        public MasteryRules ToRules()
        {
            return new MasteryRules
            {
                XpPerLevel = xpPerLevel.ToArray(),
                PointsPerLevel = pointsPerLevel,
                XpPerKill = xpPerKill,
                XpPerBossKill = xpPerBossKill,
            };
        }
    }

    public static class MasteryCatalog
    {
        public const string AssetName = "mastery";

        // The shipped rules. A missing or invalid asset falls back to the defaults so a profile never fails to load over it.
        public static MasteryRules Load()
        {
            var asset = Resources.Load<MasteryAsset>($"{DefinitionAsset.ResourcesFolder}/{AssetName}");
            if (asset == null)
            {
                Debug.LogWarning("No mastery rules asset; using the defaults.");
                return new MasteryRules();
            }

            var rules = asset.ToRules();
            var errors = rules.Validate();
            if (errors.Count > 0)
            {
                Debug.LogError("Invalid mastery rules, using the defaults:\n" + string.Join("\n", errors));
                return new MasteryRules();
            }

            return rules;
        }
    }
}
