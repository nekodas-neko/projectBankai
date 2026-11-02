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

        [MenuItem("Game/Seed arena scene")]
        public static void SeedArena()
        {
            const string materialsRoot = "Assets/Game/Art/Materials";
            const string scenePath = "Assets/Game/Scenes/Arena.unity";
            Directory.CreateDirectory(materialsRoot);

            var assets = new ArenaAssets
            {
                capsule = PrimitiveMesh(PrimitiveType.Capsule),
                cube = PrimitiveMesh(PrimitiveType.Cube),
                player = SeedMaterial($"{materialsRoot}/Player.mat", new Color(0.22f, 0.54f, 0.87f)),
                ally = SeedMaterial($"{materialsRoot}/Ally.mat", new Color(0.50f, 0.47f, 0.87f)),
                enemy = SeedMaterial($"{materialsRoot}/Enemy.mat", new Color(0.94f, 0.62f, 0.15f)),
                dead = SeedMaterial($"{materialsRoot}/Dead.mat", new Color(0.35f, 0.35f, 0.35f)),
                flash = SeedMaterial($"{materialsRoot}/Flash.mat", Color.white),
                barBackground = SeedMaterial($"{materialsRoot}/BarBackground.mat", new Color(0.1f, 0.1f, 0.12f)),
                aim = SeedMaterial($"{materialsRoot}/Aim.mat", new Color(0.78f, 0.88f, 1f)),
            };
            var ground = SeedMaterial($"{materialsRoot}/Ground.mat", new Color(0.23f, 0.25f, 0.29f));
            var rim = SeedMaterial($"{materialsRoot}/Rim.mat", new Color(0.55f, 0.58f, 0.64f));

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

            var bootstrapObject = new GameObject("ArenaBootstrap");
            bootstrapObject.AddComponent<ArenaBootstrap>().assets = assets;

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        static Material SeedMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
