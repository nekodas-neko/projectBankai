using Game.Core;
using UnityEngine;

namespace Game.Client
{
    public static class BuildInfo
    {
        public const string ResourceName = "BuildInfo";

        public static string Version
        {
            get
            {
                var asset = Resources.Load<TextAsset>(ResourceName);
                return asset != null ? asset.text.Trim() : BuildVersion.Unknown;
            }
        }
    }
}
