using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Client;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class ArtImport
    {
        public const string Root = "Assets/Game/Art/Temp";
        const string MaterialsFolder = Root + "/Materials";
        const string PrefabsFolder = Root + "/Prefabs";
        const string ControllersFolder = Root + "/Animators";
        const string SkinsRoot = "Assets/Game/Data/Resources/Skins";
        const int MaxTexture = 512;

        static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "Flying" };

        sealed class Rig
        {
            public string Folder;
            public string Model;
            public string Skin;
            public bool Playable;
            public float Scale;
            public string Idle = "Idle";
            public string Walk = "Walk";
            public string Run = "Run";
            public string[] Attacks = new string[0];
            public string Hit = "RecieveHit";
            public string Death = "Death";
        }

        static readonly Rig[] Rigs =
        {
            Character("Warrior", "tank_warrior", "Sword_Attack", "Punch", "Sword_AttackFast", "Sword_Attack", "Roll"),
            Character("Monk", "tank_monk", "Attack", "Attack2", "Attack2", "Attack", "Roll"),
            Character("Cleric", "healer_cleric", "Staff_Attack", "Spell1", "Spell2", "Spell1", "Spell2"),
            Character("Ranger", "healer_ranger", "Bow_Attack_Shoot", "Bow_Attack_Draw", "Bow_Attack_Shoot", "Bow_Attack_Shoot", "Roll"),
            Character("Wizard", "mage_wizard", "Staff_Attack", "Spell1", "Spell2", "Spell1", "Roll"),
            Character("Rogue", "mage_rogue", "Dagger_Attack", "Dagger_Attack2", "Dagger_Attack", "Dagger_Attack", "Roll"),
            Monster("GreenDemon", "boss_greendemon", 2.2f),
            Monster("Ghost", "monster_ghost", 1f),
            Monster("Skull", "monster_skull", 1f),
            Monster("Mushroom", "monster_mushroom", 1f),
            Monster("YellowDragon", "monster_dragon", 1f, "Flying", "Flying", "Flying"),
            Monster("Cthulhu", "monster_cthulhu", 1f, "Flying", "Flying", "Flying"),
        };

        static Rig Character(string model, string skin, params string[] attacks)
        {
            return new Rig { Folder = Root + "/Characters", Model = model, Skin = skin, Playable = true, Scale = 0.65f, Attacks = attacks };
        }

        static Rig Monster(string model, string skin, float scale, string idle = "Idle", string walk = "Walk", string run = "Walk")
        {
            return new Rig
            {
                Folder = Root + "/Monsters",
                Model = model,
                Skin = skin,
                Playable = false,
                Scale = scale,
                Idle = idle,
                Walk = walk,
                Run = run,
                Attacks = new[] { "Bite_Front", "Bite_InPlace", "Jump", "Bite_Front", "Jump" },
                Hit = "HitRecieve",
            };
        }

        [MenuItem("Game/Art/Import temporary art")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(MaterialsFolder);
            Directory.CreateDirectory(PrefabsFolder);
            Directory.CreateDirectory(ControllersFolder);
            Directory.CreateDirectory(SkinsRoot);

            SetTextureSizes();
            ConfigureNature();
            foreach (var rig in Rigs)
            {
                ConfigureRig(rig);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var rig in Rigs)
            {
                BuildPrefabAndSkin(rig);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SetTextureSizes()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.maxTextureSize != MaxTexture)
                {
                    importer.maxTextureSize = MaxTexture;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
        }

        static void ConfigureNature()
        {
            foreach (var path in Directory.GetFiles(Root + "/Nature", "*.fbx"))
            {
                var assetPath = path.Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                var textureFolder = Root + "/Nature/Textures";
                foreach (var name in MaterialNames(assetPath))
                {
                    var material = EnsureMaterial("Nature_" + name, textureFolder, new[] { name + "_C", name.Replace("Leaves_", "Leaf_") + "_C", name, name + "_Diffuse" }, name.Contains("Leaf") || name.Contains("Leaves") || name.Contains("Grass") || name.Contains("Flowers"));
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
                }

                importer.SaveAndReimport();
            }
        }

        static void ConfigureRig(Rig rig)
        {
            var assetPath = $"{rig.Folder}/{rig.Model}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
            var textureFolder = rig.Folder + "/Textures";
            foreach (var name in MaterialNames(assetPath))
            {
                var candidates = new[] { name, rig.Model + "_" + name, rig.Model + "_" + name + "_Texture" };
                var material = EnsureMaterial($"{rig.Model}_{name}", textureFolder, candidates, false);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                var plain = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
                clip.loopTime = LoopingClips.Contains(plain);
                clip.loopPose = clip.loopTime;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static IEnumerable<string> MaterialNames(string assetPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return model.GetComponentsInChildren<Renderer>()
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && AssetDatabase.GetAssetPath(m) == assetPath)
                .Select(m => m.name)
                .Distinct()
                .ToList();
        }

        static Material EnsureMaterial(string name, string textureFolder, string[] textureNames, bool cutout)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            Texture2D texture = null;
            foreach (var candidate in textureNames)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/{candidate}.png");
                if (texture != null)
                {
                    break;
                }
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static AnimationClip Clip(string modelPath, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview") && (c.name == name || c.name.EndsWith("|" + name)));
        }

        static void BuildPrefabAndSkin(Rig rig)
        {
            var modelPath = $"{rig.Folder}/{rig.Model}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var controllerPath = $"{ControllersFolder}/{rig.Skin}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = BuildController(rig, modelPath, controllerPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.localScale = Vector3.one * rig.Scale;
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsFolder}/{rig.Skin}.prefab");
            Object.DestroyImmediate(instance);

            var skinPath = $"{SkinsRoot}/{rig.Skin}.asset";
            var skin = AssetDatabase.LoadAssetAtPath<SkinAsset>(skinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<SkinAsset>();
                AssetDatabase.CreateAsset(skin, skinPath);
            }

            skin.playable = rig.Playable;
            skin.prefab = prefab;
            skin.scale = Vector3.one * (rig.Playable ? 1f : rig.Scale);
            EditorUtility.SetDirty(skin);
        }

        static AnimatorController BuildController(Rig rig, string modelPath, string controllerPath)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Use", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Slot", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var idle = Clip(modelPath, rig.Idle);
            var walk = Clip(modelPath, rig.Walk) ?? idle;
            var run = Clip(modelPath, rig.Run) ?? walk;
            controller.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2.5f);
            tree.AddChild(run, 6f);

            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
            machine.defaultState = locomotion;

            for (var slot = 0; slot < 5; slot++)
            {
                var name = slot < rig.Attacks.Length ? rig.Attacks[slot] : rig.Attacks[0];
                var clip = Clip(modelPath, name) ?? Clip(modelPath, rig.Attacks[0]);
                if (clip == null)
                {
                    continue;
                }

                var state = machine.AddState("Use" + slot);
                state.motion = clip;
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Use");
                enter.AddCondition(AnimatorConditionMode.Equals, slot, "Slot");
                enter.hasExitTime = false;
                enter.duration = 0.05f;
                enter.canTransitionToSelf = true;
                var exit = state.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 0.9f;
                exit.duration = 0.1f;
            }

            var hitClip = Clip(modelPath, rig.Hit);
            if (hitClip != null)
            {
                var hit = machine.AddState("Hit");
                hit.motion = hitClip;
                var enter = machine.AddAnyStateTransition(hit);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                enter.hasExitTime = false;
                enter.duration = 0.05f;
                enter.canTransitionToSelf = true;
                var exit = hit.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 0.9f;
                exit.duration = 0.1f;
            }

            var deathClip = Clip(modelPath, rig.Death);
            if (deathClip != null)
            {
                var death = machine.AddState("Death");
                death.motion = deathClip;
                var enter = machine.AddAnyStateTransition(death);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                enter.hasExitTime = false;
                enter.duration = 0.05f;
                enter.canTransitionToSelf = false;
                var revive = death.AddTransition(locomotion);
                revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                revive.hasExitTime = false;
                revive.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static void PlaceProps(Transform parent)
        {
            var random = new System.Random(7);
            var trees = Load("CommonTree_1", "CommonTree_2", "CommonTree_3", "Pine_1", "Pine_2");
            var bushes = Load("Bush_Common", "Bush_Common_Flowers");
            var cover = Load("Grass_Common_Short", "Grass_Common_Tall", "Grass_Common_Short", "Grass_Common_Tall", "Flower_3_Group", "Flower_4_Group", "Mushroom_Common", "Pebble_Round_1", "Pebble_Round_2", "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3");

            var root = new GameObject("TemporaryProps").transform;
            root.SetParent(parent, false);

            const int treeCount = 26;
            for (var i = 0; i < treeCount; i++)
            {
                var angle = (i + (float)random.NextDouble() * 0.6f) / treeCount * Mathf.PI * 2f;
                var radius = 20.5f + (float)random.NextDouble() * 2f;
                Place(root, trees[random.Next(trees.Length)], angle, radius, 0.55f + (float)random.NextDouble() * 0.3f, random);
            }

            for (var i = 0; i < 12; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Place(root, bushes[random.Next(bushes.Length)], angle, 17.5f + (float)random.NextDouble() * 2f, 0.7f + (float)random.NextDouble() * 0.4f, random);
            }

            for (var i = 0; i < 70; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var radius = 4f + Mathf.Sqrt((float)random.NextDouble()) * 13f;
                Place(root, cover[random.Next(cover.Length)], angle, radius, 0.8f + (float)random.NextDouble() * 0.6f, random);
            }
        }

        static GameObject[] Load(params string[] names)
        {
            return names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Nature/{n}.fbx")).Where(m => m != null).ToArray();
        }

        static void Place(Transform parent, GameObject model, float angle, float radius, float scale, System.Random random)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            instance.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic);
        }

        [MenuItem("Game/Art/Report temporary art")]
        public static void Report()
        {
            AssetDatabase.Refresh();
            var text = new StringBuilder();
            foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
                    .Select(m => $"{m.name}:{(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "NO-TEX")}");
                text.AppendLine($"{Path.GetFileName(path)} size={bounds.size.ToString("0.00")} materials=[{string.Join("; ", materials)}]");
            }

            Debug.Log("ART REPORT\n" + text);
        }
    }
}
