using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    public static class DefinitionLoader
    {
        public const string TuningFileName = "tuning.txt";

        // Tests set this to feed a tuning file without touching the disk; null means read the real file (development builds only).
        public static string TuningText { get; set; }

        // "" when no tuning is active, otherwise one line for the build label; Changes counts updates so a label can refresh cheaply.
        public static string TuningStatus { get; private set; } = string.Empty;

        public static int TuningChanges { get; private set; }

        public static void Load(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities)
        {
            Load(out classes, out abilities, out _);
        }

        public static void Load(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities, out Registry<DungeonDef> dungeons)
        {
            Build(out classes, out abilities, out dungeons);
            var errors = Validate(classes, abilities, dungeons);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Invalid definitions:\n" + string.Join("\n", errors));
            }

            var text = TuningText ?? ReadTuningFile();
            if (string.IsNullOrEmpty(text))
            {
                SetStatus(string.Empty);
                return;
            }

            Build(out var tunedClasses, out var tunedAbilities, out var tunedDungeons);
            var problems = new List<string>();
            var entries = Tuning.Parse(text, problems);
            Tuning.Apply(entries, tunedAbilities, tunedClasses, problems);
            if (problems.Count == 0)
            {
                problems = Validate(tunedClasses, tunedAbilities, tunedDungeons);
            }

            if (problems.Count > 0)
            {
                Debug.LogError("Tuning file ignored:\n" + string.Join("\n", problems));
                SetStatus($"tuning IGNORED: {problems[0]}");
                return;
            }

            classes = tunedClasses;
            abilities = tunedAbilities;
            dungeons = tunedDungeons;
            Debug.Log($"Tuning: {entries.Count} overrides applied");
            SetStatus($"tuning: {entries.Count} overrides");
        }

        static void Build(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities, out Registry<DungeonDef> dungeons)
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
        }

        static List<string> Validate(Registry<ClassDef> classes, Registry<AbilityDef> abilities, Registry<DungeonDef> dungeons)
        {
            var errors = DefinitionValidator.Validate(classes, abilities);
            errors.AddRange(DefinitionValidator.ValidateDungeons(dungeons, classes));
            return errors;
        }

        static string ReadTuningFile()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return null;
            }

            try
            {
                var path = Path.Combine(Application.persistentDataPath, TuningFileName);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }

                if (Application.isEditor)
                {
                    var project = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, TuningFileName);
                    return File.Exists(project) ? File.ReadAllText(project) : null;
                }
            }
            catch (IOException exception)
            {
                Debug.LogWarning("Tuning file could not be read: " + exception.Message);
            }

            return null;
        }

        static void SetStatus(string status)
        {
            if (status != TuningStatus)
            {
                TuningStatus = status;
                TuningChanges++;
            }
        }
    }
}
