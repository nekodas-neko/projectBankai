using UnityEngine;

namespace Game.Client
{
    public abstract class DefinitionAsset : ScriptableObject
    {
        public const string ResourcesFolder = "Definitions";

        public string Id => name;
    }
}
