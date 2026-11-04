using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The entities of one simulation, and the queries and rules that apply to all of them.
    internal sealed class World
    {
        readonly ServerConfig config;
        readonly Registry<ClassDef> classes;
        int nextEntityId = 1;

        public World(ServerConfig config, Registry<ClassDef> classes)
        {
            this.config = config;
            this.classes = classes;
        }

        public List<Entity> Entities { get; } = new List<Entity>();

        public List<DamageEvent> Events { get; } = new List<DamageEvent>();

        public List<UseEvent> Uses { get; } = new List<UseEvent>();

        public static Vec2 SpawnPoint(int index)
        {
            return new Vec2(((index % 4) - 1.5f) * 2f, 0f);
        }

        public Entity Find(EntityId id)
        {
            for (var i = 0; i < Entities.Count; i++)
            {
                if (Entities[i].Id == id)
                {
                    return Entities[i];
                }
            }

            return null;
        }

        public Entity Spawn(PlayerId owner, Team team, string classId, Vec2 position, int appearance)
        {
            var entity = Entity.FromClass(new EntityId(nextEntityId++), owner, team, classes.Get(classId), position, appearance);
            Entities.Add(entity);
            return entity;
        }

        public void Remove(Predicate<Entity> match)
        {
            Entities.RemoveAll(match);
        }

        public Vec2 InsideArena(Vec2 position)
        {
            return config.Simulation.Constrain(position);
        }

        public Entity NearestHostile(Entity user, float range)
        {
            Entity best = null;
            var bestSqr = range * range;
            for (var i = 0; i < Entities.Count; i++)
            {
                var other = Entities[i];
                if (!other.IsAlive || !Entity.AreHostile(user, other))
                {
                    continue;
                }

                var sqr = (other.Position - user.Position).SqrLength;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = other;
                }
            }

            return best;
        }

        // What a tapped attack aims at: a boss in range first (so adds in the way do not steal the aim), otherwise the nearest.
        public Entity PreferredTarget(Entity user, float range)
        {
            Entity boss = null;
            var bossSqr = range * range;
            for (var i = 0; i < Entities.Count; i++)
            {
                var other = Entities[i];
                if (!other.IsBoss || !other.IsAlive || !Entity.AreHostile(user, other))
                {
                    continue;
                }

                var sqr = (other.Position - user.Position).SqrLength;
                if (sqr <= bossSqr)
                {
                    bossSqr = sqr;
                    boss = other;
                }
            }

            return boss ?? NearestHostile(user, range);
        }

        public void Separate()
        {
            for (var i = 0; i < Entities.Count; i++)
            {
                var a = Entities[i];
                if (!a.IsAlive || a.Radius <= 0f || a.HasStatus(StatusType.Dashing))
                {
                    continue;
                }

                for (var j = i + 1; j < Entities.Count; j++)
                {
                    var b = Entities[j];
                    if (!b.IsAlive || b.Radius <= 0f || b.HasStatus(StatusType.Dashing))
                    {
                        continue;
                    }

                    var minimum = a.Radius + b.Radius;
                    var offset = b.Position - a.Position;
                    var distance = offset.Length;
                    if (distance >= minimum)
                    {
                        continue;
                    }

                    var normal = distance > 1e-4f ? offset * (1f / distance) : new Vec2(1f, 0f);
                    var push = (minimum - distance) * 0.5f;
                    a.Position = InsideArena(a.Position - normal * push);
                    b.Position = InsideArena(b.Position + normal * push);
                }
            }
        }
    }
}
