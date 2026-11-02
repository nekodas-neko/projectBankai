using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [CreateAssetMenu(menuName = "Game/Skin", fileName = "new_skin")]
    public class SkinAsset : ScriptableObject
    {
        public const string ResourcesFolder = "Skins";

        public enum BodyShape
        {
            Capsule,
            Sphere,
        }

        public bool playable = true;
        public BodyShape shape = BodyShape.Capsule;
        [Min(0f)] public float bobHeight = 0.14f;
        public GameObject prefab;
        public Material body;
        public Material accent;
        public Vector3 scale = Vector3.one;

        public string Id => name;

        public int Appearance => StableId.From(name);
    }
}
