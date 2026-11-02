using System.IO;
using Game.Client;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SeedContent
    {
        const string Root = "Assets/Game/Data/Resources/Definitions";

        [MenuItem("Game/Seed default definitions")]
        public static void SeedDefinitions()
        {
            Directory.CreateDirectory(Root);

            var slash = Ability("slash", cooldown: 0.5f, range: 2.5f, radius: 0.8f, damage: 10f);
            var bolt = Ability("bolt", cooldown: 2f, range: 12f, radius: 0.6f, damage: 25f);
            var shockwave = Ability("shockwave", cooldown: 5f, range: 4f, radius: 4f, damage: 15f);

            Class("fighter", Role.Dps, 100f, 6f, 1f, slash, bolt, shockwave);
            Class("training_dummy", Role.None, 400f, 0f, 1f, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static AbilityAsset Ability(string id, float cooldown, float range, float radius, float damage)
        {
            var path = $"{Root}/{id}.asset";
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

        static void Class(string id, Role role, float health, float speed, float power, AbilityAsset auto, params AbilityAsset[] skills)
        {
            var path = $"{Root}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ClassAsset>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<ClassAsset>();
            asset.role = role;
            asset.maxHealth = health;
            asset.moveSpeed = speed;
            asset.power = power;
            asset.autoAttack = auto;
            asset.skills = skills;
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
