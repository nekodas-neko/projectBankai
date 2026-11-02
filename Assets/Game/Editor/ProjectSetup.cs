using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    public static class ProjectSetup
    {
        const string ApplicationId = "com.projectbankai.game";

        public static void Apply()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            PlayerSettings.companyName = "projectBankai";
            PlayerSettings.productName = "projectBankai";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationId);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup applied");
        }
    }
}
