using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class BuildScript
    {
        const string DefaultOutput = "Builds/Android/projectBankai.apk";

        public static void BuildAndroid()
        {
            var output = Argument("-outputPath") ?? DefaultOutput;
            var development = Array.IndexOf(Environment.GetCommandLineArgs(), "-development") >= 0;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            var version = BuildInfoWriter.Write();
            var originalVersionCode = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, BuildInfoWriter.CommitCount());
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            PlayerSettings.Android.bundleVersionCode = originalVersionCode;
            AssetDatabase.SaveAssets();
            var succeeded = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"Android build {version} {report.summary.result}: {output} ({report.summary.totalSize} bytes, {report.summary.totalErrors} errors)");
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
