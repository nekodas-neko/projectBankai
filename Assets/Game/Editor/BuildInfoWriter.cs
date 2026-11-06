using System.Diagnostics;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    [InitializeOnLoad]
    public class BuildInfoWriter : IPreprocessBuildWithReport
    {
        const string AssetPath = "Assets/Game/Client/Resources/BuildInfo.txt";

        static BuildInfoWriter()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    Write();
                }
            };
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Write();
        }

        public static int CommitCount()
        {
            return int.TryParse(Git("rev-list --count HEAD"), out var count) ? count : 0;
        }

        public static string Write()
        {
            var version = BuildVersion.Format(CommitCount(), Git("rev-parse --short HEAD"));

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            if (!File.Exists(AssetPath) || File.ReadAllText(AssetPath) != version)
            {
                File.WriteAllText(AssetPath, version);
                AssetDatabase.ImportAsset(AssetPath);
            }

            return version;
        }

        static string Git(string arguments)
        {
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
