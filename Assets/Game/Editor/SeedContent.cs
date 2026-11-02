using System.IO;
using Game.Client;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SeedContent
    {
        const string DefinitionsRoot = "Assets/Game/Data/Resources/Definitions";
        const string SkinsRoot = "Assets/Game/Data/Resources/Skins";
        const string MaterialsRoot = "Assets/Game/Art/Materials";
        const string EffectsRoot = "Assets/Game/Art/Effects";

        [MenuItem("Game/Seed everything")]
        public static void SeedAll()
        {
            SeedDefinitions();
            SeedCosmetics();
            SeedArena();
        }

        [MenuItem("Game/Seed default definitions")]
        public static void SeedDefinitions()
        {
            Directory.CreateDirectory(DefinitionsRoot);

            foreach (var obsolete in new[] { "fighter", "training_dummy", "slash", "bolt", "shockwave" })
            {
                AssetDatabase.DeleteAsset($"{DefinitionsRoot}/{obsolete}.asset");
            }

            var swordSlash = Ability("sword_slash", cooldown: 0.6f, range: 2.8f, radius: 1.2f, damage: 9f);
            var shieldBash = Ability("shield_bash", cooldown: 6f, range: 3.5f, radius: 1.8f, damage: 14f);
            var holyBolt = Ability("holy_bolt", cooldown: 0.9f, range: 12f, radius: 0.5f, damage: 6f);
            var radiantPulse = Ability("radiant_pulse", cooldown: 5f, range: 5f, radius: 3f, damage: 10f);
            var arcaneBolt = Ability("arcane_bolt", cooldown: 0.8f, range: 14f, radius: 0.5f, damage: 9f);
            var fireball = Ability("fireball", cooldown: 3f, range: 18f, radius: 1f, damage: 30f);
            var slimeSlam = Ability("slime_slam", cooldown: 2f, range: 2f, radius: 1.2f, damage: 8f);

            Class("tank", Role.Tank, 180f, 5f, 1f, swordSlash, shieldBash);
            Class("healer", Role.Healer, 100f, 6f, 1f, holyBolt, radiantPulse);
            Class("mage", Role.Dps, 80f, 6.5f, 1.2f, arcaneBolt, fireball);
            Class("slime", Role.None, 60f, 0f, 1f, slimeSlam);
            Class("slime_boss", Role.None, 800f, 0f, 1f, slimeSlam);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/Seed placeholder skins and effects")]
        public static void SeedCosmetics()
        {
            Directory.CreateDirectory(SkinsRoot);
            Directory.CreateDirectory($"{MaterialsRoot}/Skins");
            Directory.CreateDirectory(EffectsRoot);

            Skin("default", new Color(0.22f, 0.54f, 0.87f), new Color(0.7f, 0.85f, 1f), new Vector3(1f, 1f, 1f), false);
            Skin("tank_steel", new Color(0.55f, 0.6f, 0.68f), new Color(0.95f, 0.8f, 0.3f), new Vector3(1.15f, 1.05f, 1.15f), true);
            Skin("tank_gold", new Color(0.9f, 0.72f, 0.2f), new Color(0.55f, 0.6f, 0.68f), new Vector3(1.15f, 1.05f, 1.15f), true);
            Skin("healer_light", new Color(0.93f, 0.92f, 0.85f), new Color(1f, 0.85f, 0.3f), new Vector3(0.95f, 1f, 0.95f), true);
            Skin("healer_verdant", new Color(0.2f, 0.62f, 0.55f), new Color(0.93f, 0.92f, 0.85f), new Vector3(0.95f, 1f, 0.95f), true);
            Skin("mage_arcane", new Color(0.45f, 0.3f, 0.8f), new Color(0.4f, 0.85f, 1f), new Vector3(0.9f, 1.05f, 0.9f), true);
            Skin("mage_crimson", new Color(0.7f, 0.15f, 0.3f), new Color(1f, 0.8f, 0.3f), new Vector3(0.9f, 1.05f, 0.9f), true);
            Skin("slime_green", new Color(0.35f, 0.8f, 0.5f), new Color(0.8f, 1f, 0.85f), new Vector3(1.4f, 1.1f, 1.4f), false, SkinAsset.BodyShape.Sphere, 0.25f);
            Skin("slime_king", new Color(0.3f, 0.75f, 0.6f), new Color(0.95f, 0.8f, 0.25f), new Vector3(3.2f, 2.6f, 3.2f), false, SkinAsset.BodyShape.Sphere, 0.4f);

            var cube = PrimitiveMesh(PrimitiveType.Cube);
            var slash = Burst("fx_cast_slash", new Color(1f, 0.95f, 0.75f), 18, 9f, 0.16f, 0.25f, 35f, cube);
            var shield = Burst("fx_cast_shield", new Color(0.95f, 0.8f, 0.3f), 22, 7f, 0.22f, 0.3f, 45f, cube);
            var holy = Burst("fx_cast_holy", new Color(1f, 0.95f, 0.5f), 20, 8f, 0.16f, 0.35f, 25f, cube);
            var pulse = Burst("fx_cast_pulse", new Color(1f, 0.95f, 0.6f), 40, 6f, 0.18f, 0.5f, 0f, cube);
            var arcane = Burst("fx_cast_arcane", new Color(0.55f, 0.75f, 1f), 24, 11f, 0.14f, 0.35f, 20f, cube);
            var fire = Burst("fx_cast_fire", new Color(1f, 0.55f, 0.15f), 30, 12f, 0.2f, 0.4f, 25f, cube);
            var slime = Burst("fx_cast_slime", new Color(0.4f, 0.9f, 0.55f), 14, 5f, 0.18f, 0.3f, 40f, cube);
            var hitSpark = Burst("fx_hit_spark", new Color(1f, 0.95f, 0.7f), 14, 6f, 0.14f, 0.3f, 0f, cube);
            var hitHoly = Burst("fx_hit_holy", new Color(1f, 0.95f, 0.5f), 16, 5f, 0.14f, 0.4f, 0f, cube);
            var hitArcane = Burst("fx_hit_arcane", new Color(0.6f, 0.75f, 1f), 16, 6f, 0.14f, 0.35f, 0f, cube);
            var hitFire = Burst("fx_hit_fire", new Color(1f, 0.5f, 0.1f), 28, 7f, 0.2f, 0.45f, 0f, cube);
            var hitSlime = Burst("fx_hit_slime", new Color(0.4f, 0.9f, 0.55f), 12, 4f, 0.16f, 0.35f, 0f, cube);

            Link("sword_slash", slash, hitSpark);
            Link("shield_bash", shield, hitSpark);
            Link("holy_bolt", holy, hitHoly);
            Link("radiant_pulse", pulse, hitHoly);
            Link("arcane_bolt", arcane, hitArcane);
            Link("fireball", fire, hitFire);
            Link("slime_slam", slime, hitSlime);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/Seed arena scene")]
        public static void SeedArena()
        {
            const string scenePath = "Assets/Game/Scenes/Arena.unity";
            Directory.CreateDirectory(MaterialsRoot);

            var assets = new ArenaAssets
            {
                capsule = PrimitiveMesh(PrimitiveType.Capsule),
                sphere = PrimitiveMesh(PrimitiveType.Sphere),
                cube = PrimitiveMesh(PrimitiveType.Cube),
                cylinder = PrimitiveMesh(PrimitiveType.Cylinder),
                player = SeedMaterial($"{MaterialsRoot}/Player.mat", new Color(0.22f, 0.54f, 0.87f)),
                ally = SeedMaterial($"{MaterialsRoot}/Ally.mat", new Color(0.50f, 0.47f, 0.87f)),
                enemy = SeedMaterial($"{MaterialsRoot}/Enemy.mat", new Color(0.94f, 0.62f, 0.15f)),
                dead = SeedMaterial($"{MaterialsRoot}/Dead.mat", new Color(0.35f, 0.35f, 0.35f)),
                flash = SeedMaterial($"{MaterialsRoot}/Flash.mat", Color.white),
                barBackground = SeedMaterial($"{MaterialsRoot}/BarBackground.mat", new Color(0.1f, 0.1f, 0.12f)),
                aim = SeedMaterial($"{MaterialsRoot}/Aim.mat", new Color(0.78f, 0.88f, 1f)),
            };
            var ground = SeedMaterial($"{MaterialsRoot}/Ground.mat", new Color(0.23f, 0.25f, 0.29f));
            var rim = SeedMaterial($"{MaterialsRoot}/Rim.mat", new Color(0.55f, 0.58f, 0.64f));

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

        static Material SeedMaterial(string path, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(shader));
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.2f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Skin(string id, Color body, Color accent, Vector3 scale, bool playable, SkinAsset.BodyShape shape = SkinAsset.BodyShape.Capsule, float bob = 0.14f)
        {
            var path = $"{SkinsRoot}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkinAsset>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<SkinAsset>();
            asset.playable = playable;
            asset.shape = shape;
            asset.bobHeight = bob;
            asset.scale = scale;
            asset.body = SeedMaterial($"{MaterialsRoot}/Skins/{id}_body.mat", body);
            asset.accent = SeedMaterial($"{MaterialsRoot}/Skins/{id}_accent.mat", accent);
            AssetDatabase.CreateAsset(asset, path);
        }

        static GameObject Burst(string id, Color color, int count, float speed, float size, float life, float coneAngle, Mesh mesh)
        {
            var path = $"{EffectsRoot}/{id}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(id);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size * 2.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = coneAngle > 0f ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
            shape.angle = coneAngle;
            shape.radius = 0.15f;

            var sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = SeedMaterial($"{MaterialsRoot}/Fx_{id}.mat", color, "Universal Render Pipeline/Unlit");

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void Link(string abilityId, GameObject cast, GameObject hit)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>($"{DefinitionsRoot}/{abilityId}.asset");
            if (asset == null)
            {
                return;
            }

            if (asset.castEffect == null)
            {
                asset.castEffect = cast;
            }

            if (asset.hitEffect == null)
            {
                asset.hitEffect = hit;
            }

            EditorUtility.SetDirty(asset);
        }

        static AbilityAsset Ability(string id, float cooldown, float range, float radius, float damage)
        {
            var path = $"{DefinitionsRoot}/{id}.asset";
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
            var path = $"{DefinitionsRoot}/{id}.asset";
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
