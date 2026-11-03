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

            // An ultimate is a moment in a fight, not a rotation.
            public float MaxUltimatesPerFight { get; set; } = 2f;

            // Played the way it is meant to be played, nobody should finish a fight nearly dead.
            public float MinHealthLeftWhenPlayedWell { get; set; } = 0.3f;
        }

        public static List<string> Misses(IReadOnlyList<BalanceResult> results, Targets targets)
        {
            var misses = new List<string>();
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (r.Policy == "stand still")
                {
                    continue;
                }

                var who = $"{r.Weapon} ({r.Policy}) vs {r.Boss}";
                var shortest = targets.MinFightSeconds.TryGetValue(r.Boss, out var value) ? value : targets.DefaultMinFightSeconds;
                if (r.BossDead && r.Seconds < shortest)
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: over in {1:0.0}s, target at least {2:0}s", who, r.Seconds, shortest));
                }

                if (r.UltimatesFired > targets.MaxUltimatesPerFight)
                {
                    misses.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:0} ultimates, target at most {2:0}", who, r.UltimatesFired, targets.MaxUltimatesPerFight));
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
