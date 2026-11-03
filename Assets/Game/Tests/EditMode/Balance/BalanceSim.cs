using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core.Tests
{
    public sealed class BalanceResult
    {
        public string Weapon;
        public string Boss;
        public string Policy;
        public bool Survived;
        public bool BossDead;
        public float Seconds;
        public float HealthLeft;
        public float MaxHealth;
        public float DamageTaken;
        public float DamageDealt;
        public float UltimatesFired;
    }

    // Plays one weapon against one boss in the plain-C# server, through the same client API a phone uses,
    // and reports what happened. Nothing here knows about Unity, so it runs in the fast tests and on the shipped data.
    public static class BalanceSim
    {
        public const float Step = 1f / 30f;

        public static BalanceResult Run(Registry<ClassDef> classes, Registry<AbilityDef> abilities, string weapon, string boss, IBotPolicy policy, float maxSeconds = 240f, ulong seed = 1)
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            var client = session.AddPlayer(new PlayerId(1), weapon);
            session.Server.SpawnNpc(boss, Team.Enemies, new Vec2(0f, 8f));

            // A tank is not the player a boss picks for its "farthest player" mechanics; a ranged friend is. Alone, the tank
            // would soak all of them, so it gets an idle ally that stays behind and cannot die, to take them instead.
            if (policy is TankBlocker && classes.TryGet("staff", out _))
            {
                session.AddPlayer(new PlayerId(2), "staff");
                var ally = session.Server.FindPlayerEntity(new PlayerId(2));
                var stats = ally.Stats;
                stats.MaxHealth = 1e6f;
                ally.Stats = stats;
                ally.Health = 1e6f;
            }
            var view = new BotView(client, abilities, classes.Get(weapon)) { EnemyRadius = classes.Get(boss).BodyRadius };
            var result = new BalanceResult { Weapon = weapon, Boss = boss, Policy = policy.Name };
            var events = new List<DamageEvent>();
            var uses = new List<UseEvent>();
            var maxHealth = classes.Get(weapon).Stats.MaxHealth;
            var ultimateId = classes.Get(weapon).Skills.Length > Entity.UltimateSlot - 1 ? StableId.From(classes.Get(weapon).Skills[Entity.UltimateSlot - 1]) : 0;
            var me = default(EntityState);
            var ticks = (int)(maxSeconds / Step);

            for (var tick = 0; tick < ticks; tick++)
            {
                policy.Step(view, Step);
                session.Advance(Step);

                events.Clear();
                client.DrainEvents(events);
                for (var i = 0; i < events.Count; i++)
                {
                    var hit = events[i];
                    if (hit.Outcome == HitOutcome.Healed)
                    {
                        continue;
                    }

                    if (hit.Target == client.LocalEntity)
                    {
                        result.DamageTaken += hit.Amount;
                    }
                    else if (hit.Source == client.LocalEntity)
                    {
                        result.DamageDealt += hit.Amount;
                    }
                }

                uses.Clear();
                client.DrainUses(uses);
                for (var i = 0; i < uses.Count; i++)
                {
                    if (uses[i].User == client.LocalEntity && ultimateId != 0 && uses[i].Ability == ultimateId)
                    {
                        result.UltimatesFired++;
                    }
                }

                result.Seconds = (tick + 1) * Step;
                if (client.TryGetLocalState(out me))
                {
                    if (me.Health <= 0f)
                    {
                        break;
                    }
                }

                if (EnemiesAlive(session.Server) == 0)
                {
                    result.BossDead = true;
                    break;
                }
            }

            result.Survived = client.TryGetLocalState(out me) && me.Health > 0f;
            result.HealthLeft = result.Survived && maxHealth > 0f ? me.Health / maxHealth : 0f;
            result.MaxHealth = maxHealth;
            return result;
        }

        static int EnemiesAlive(GameServer server)
        {
            var count = 0;
            foreach (var entity in server.Entities)
            {
                if (!entity.Owner.IsValid && entity.IsAlive && entity.Team == Team.Enemies)
                {
                    count++;
                }
            }

            return count;
        }

        // The design targets the report is checked against. They are goals to tune toward, not rules the tests enforce.
        public sealed class Targets
        {
            // A perfect bot kills faster than a person, so a fight should last at least this long for it, per boss (seconds).
            public Dictionary<string, float> MinFightSeconds { get; } = new Dictionary<string, float>();

            public float DefaultMinFightSeconds { get; set; } = 40f;

            // An ultimate is a moment in a fight, not a rotation: at most this many a minute.
            public float MaxUltimatesPerMinute { get; set; } = 1.5f;

            // Played the way it is meant to be played, nobody should finish a fight nearly dead.
            public float MinHealthLeftWhenPlayedWell { get; set; } = 0.3f;

            // Played the intended way, a weapon should still be hit now and then: a perfect style that takes no damage
            // at all means the boss is not asking anything of it. At least this fraction of its health over a fight.
            public float MinDamageTakenFraction { get; set; } = 0.10f;

            // Even a slow weapon should finish a boss alone within this long (seconds); a timeout means it cannot.
            public float MaxFightSeconds { get; set; } = 180f;

            // The way each weapon is meant to be played. A weapon with an entry is only judged on that policy,
            // because a staff that stands and blocks is not a style anyone is asking for.
            public Dictionary<string, string> IntendedPolicy { get; } = new Dictionary<string, string>();
        }

        public static List<string> Misses(IReadOnlyList<BalanceResult> results, Targets targets)
        {
            var misses = new List<string>();
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (r.Policy == "stand still" || (targets.IntendedPolicy.TryGetValue(r.Weapon, out var intended) && intended != r.Policy))
                {
                    continue;
                }

                var who = $"{r.Weapon} ({r.Policy}) vs {r.Boss}";
                var shortest = targets.MinFightSeconds.TryGetValue(r.Boss, out var value) ? value : targets.DefaultMinFightSeconds;
                if (r.BossDead && r.Seconds < shortest)
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: over in {1:0.0}s, target at least {2:0}s", who, r.Seconds, shortest));
                }

                if (r.Survived && r.BossDead && r.MaxHealth > 0f && r.DamageTaken / r.MaxHealth < targets.MinDamageTakenFraction)
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: too safe, only {1:P0} of its health taken, target at least {2:P0}", who, r.DamageTaken / r.MaxHealth, targets.MinDamageTakenFraction));
                }

                if (r.Survived && (!r.BossDead || r.Seconds > targets.MaxFightSeconds))
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: not finished in {1:0}s, target at most {2:0}s", who, r.Seconds, targets.MaxFightSeconds));
                }

                var perMinute = r.Seconds > 0f ? r.UltimatesFired * 60f / r.Seconds : 0f;
                if (perMinute > targets.MaxUltimatesPerMinute)
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.0} ultimates a minute ({2:0} in {3:0}s), target at most {4:0.0}", who, perMinute, r.UltimatesFired, r.Seconds, targets.MaxUltimatesPerMinute));
                }

                if (!r.Survived || (r.BossDead && r.HealthLeft < targets.MinHealthLeftWhenPlayedWell))
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}, target at least {2:P0} health left", who, r.Survived ? $"only {r.HealthLeft:P0} left" : "died", targets.MinHealthLeftWhenPlayedWell));
                }
            }

            return misses;
        }

        public static string Table(IReadOnlyList<BalanceResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("weapon              boss              policy           result     time  hp left  taken  dealt  ults");
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var outcome = r.BossDead && r.Survived ? "WIN" : r.Survived ? "timeout" : "DEAD";
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-19} {1,-17} {2,-16} {3,-8} {4,5:0.0}s {5,7:P0} {6,6:0} {7,6:0} {8,5:0}",
                    r.Weapon, r.Boss, r.Policy, outcome, r.Seconds, r.HealthLeft, r.DamageTaken, r.DamageDealt, r.UltimatesFired));
            }

            return text.ToString();
        }
    }
}
