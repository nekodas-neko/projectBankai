using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [CreateAssetMenu(menuName = "Game/Ability", fileName = "new_ability")]
    public class AbilityAsset : DefinitionAsset
    {
        [Min(0f)] public float cooldown = 1f;
        [Min(0.1f)] public float range = 3f;
        [Min(0f)] public float radius = 0.5f;
        [Min(0f)] public float damage = 10f;
        public GameObject castEffect;
        public GameObject hitEffect;

        public AbilityDef ToDefinition()
        {
            return new AbilityDef { Id = Id, Cooldown = cooldown, Range = range, Radius = radius, Damage = damage };
        }
    }
}
