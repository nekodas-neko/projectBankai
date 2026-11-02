using UnityEngine;

namespace Game.Client
{
    public static class EffectPlayer
    {
        const float DefaultLifetime = 2f;

        public static GameObject Play(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, position, rotation);
            Object.Destroy(instance, Lifetime(instance));
            return instance;
        }

        static float Lifetime(GameObject instance)
        {
            var longest = 0f;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>())
            {
                var main = system.main;
                longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
            }

            return longest > 0f ? longest + 0.1f : DefaultLifetime;
        }
    }
}
