using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    public static class SkinCatalog
    {
        public const string DefaultSkin = "default";

        static Dictionary<int, SkinAsset> byAppearance;
        static List<SkinAsset> playable;

        public static IReadOnlyList<SkinAsset> Playable
        {
            get
            {
                Ensure();
                return playable;
            }
        }

        public static IReadOnlyList<SkinAsset> ForClass(string classId)
        {
            Ensure();
            var result = new List<SkinAsset>();
            for (var i = 0; i < playable.Count; i++)
            {
                if (playable[i].name.StartsWith(classId + "_", System.StringComparison.Ordinal))
                {
                    result.Add(playable[i]);
                }
            }

            return result.OrderBy(skin => skin.prefab == null).ToList();
        }

        public static SkinAsset Get(int appearance)
        {
            Ensure();
            if (byAppearance.TryGetValue(appearance, out var skin))
            {
                return skin;
            }

            return byAppearance.TryGetValue(StableId.From(DefaultSkin), out var fallback) ? fallback : null;
        }

        static void Ensure()
        {
            if (byAppearance != null)
            {
                return;
            }

            byAppearance = new Dictionary<int, SkinAsset>();
            playable = new List<SkinAsset>();
            foreach (var skin in Resources.LoadAll<SkinAsset>(SkinAsset.ResourcesFolder))
            {
                byAppearance[skin.Appearance] = skin;
                if (skin.playable)
                {
                    playable.Add(skin);
                }
            }

            playable.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }
    }

    public static class AbilityCatalog
    {
        static Dictionary<int, AbilityAsset> byId;

        public static bool TryGet(int abilityId, out AbilityAsset asset)
        {
            if (byId == null)
            {
                byId = new Dictionary<int, AbilityAsset>();
                foreach (var ability in Resources.LoadAll<AbilityAsset>(DefinitionAsset.ResourcesFolder))
                {
                    byId[StableId.From(ability.Id)] = ability;
                }
            }

            return byId.TryGetValue(abilityId, out asset);
        }
    }
}
