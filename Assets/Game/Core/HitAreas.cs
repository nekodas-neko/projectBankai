using System;

namespace Game.Core
{
    // Where an attack lands: used by the server to decide who is hit, and by anything (a bot, a warning) that needs the same answer.
    public static class HitAreas
    {
        public static float Reach(AbilityDef ability)
        {
            return ability.Shape == AbilityShape.Circle && !ability.Everyone ? ability.Radius : ability.Range;
        }

        public static bool Contains(AbilityDef ability, Vec2 origin, Vec2 direction, Vec2 point, float bodyRadius)
        {
            if (ability.Everyone)
            {
                return true;
            }

            var offset = point - origin;
            switch (ability.Shape)
            {
                case AbilityShape.Circle:
                    return offset.Length <= ability.Radius + bodyRadius;
                case AbilityShape.Cone:
                    var distance = offset.Length;
                    if (distance - bodyRadius > ability.Range)
                    {
                        return false;
                    }

                    if (distance < 1e-4f)
                    {
                        return true;
                    }

                    return Vec2.Dot(offset * (1f / distance), direction) >= MathF.Cos(ability.Angle * 0.5f * (MathF.PI / 180f));
                default:
                    var along = Vec2.Dot(offset, direction);
                    if (along + bodyRadius < 0f || along - bodyRadius > ability.Range)
                    {
                        return false;
                    }

                    return (offset - direction * along).Length <= ability.Radius + bodyRadius;
            }
        }
    }
}
