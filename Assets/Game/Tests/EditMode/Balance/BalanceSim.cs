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

        // How far the player walked over the fight (world units).
        public float Moved;

        // Boss casts this player stopped.
        public int Interrupts;
    }

    // Plays one weapon against one boss in the plain-C# server, through the same client API a phone uses,
    // and reports what happened. Nothing here knows about Unity, so it runs in the fast tests and on the shipped data.
    public static class BalanceSim
    {
        public const float Step = 1f / 30f;

        public static BalanceResult Run(Registry<ClassDef> classes, Registry<AbilityDef> abilities, string weapon, string boss, IBotPolicy policy, float maxSeconds = 240f, ulong seed = 1, int[] loadout = null)
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            var client = session.AddPlayer(new PlayerId(1), weapon, 0, loadout);
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

            var tracker = new Tracker(client, abilities, classes, weapon, boss, policy);
            return Play(session, new[] { tracker }, maxSeconds)[0];
        }

        // A party fight: the tank holds its ground at the front and the ranged player stands back, both against one boss.
        // Returns the tank's result first and the ranged player's second.
        public static BalanceResult[] RunParty(Registry<ClassDef> classes, Registry<AbilityDef> abilities, string tankWeapon, IBotPolicy tankPolicy, string rangedWeapon, IBotPolicy rangedPolicy, string boss, float maxSeconds = 240f, ulong seed = 1, StringBuilder trace = null)
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            var tank = session.AddPlayer(new PlayerId(1), tankWeapon);
            var ranged = session.AddPlayer(new PlayerId(2), rangedWeapon);
            session.Server.SpawnNpc(boss, Team.Enemies, new Vec2(0f, 8f));
            var trackers = new[]
            {
                new Tracker(tank, abilities, classes, tankWeapon, boss, tankPolicy),
                new Tracker(ranged, abilities, classes, rangedWeapon, boss, rangedPolicy),
            };
            return Play(session, trackers, maxSeconds, trace);
        }

        // A party of any size against one boss, in a one-room dungeon so the boss gets the real party-size health scale
        // (healthPerExtraPlayer 0 means no scaling). Results come back in the order of the members.
        public static BalanceResult[] RunParty(Registry<ClassDef> classes, Registry<AbilityDef> abilities, IReadOnlyList<KeyValuePair<string, IBotPolicy>> members, string boss, float healthPerExtraPlayer, float maxSeconds = 300f, ulong seed = 1)
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, seed);
            var trackers = new Tracker[members.Count];
            for (var i = 0; i < members.Count; i++)
            {
                var client = session.AddPlayer(new PlayerId(i + 1), members[i].Key);
                trackers[i] = new Tracker(client, abilities, classes, members[i].Key, boss, members[i].Value);
            }

            session.StartDungeon(new DungeonDef
            {
                Id = "balance",
                HealthPerExtraPlayer = healthPerExtraPlayer,
                Rooms = new[] { new RoomDef { Spawns = new[] { new SpawnDef { Class = boss, X = 0f, Y = 8f, Boss = true } } } },
            });
            return Play(session, trackers, maxSeconds);
        }

        static BalanceResult[] Play(LocalSession session, Tracker[] trackers, float maxSeconds, StringBuilder trace = null)
        {
            var ticks = (int)(maxSeconds / Step);
            var bossDead = false;
            var seconds = 0f;
            for (var tick = 0; tick < ticks; tick++)
            {
                for (var i = 0; i < trackers.Length; i++)
                {
                    trackers[i].Policy.Step(trackers[i].View, Step);
                }

                session.Advance(Step);

                var anyAlive = false;
                for (var i = 0; i < trackers.Length; i++)
                {
                    trackers[i].Observe();
                    anyAlive |= trackers[i].Alive;
                }

                seconds = (tick + 1) * Step;
                if (trace != null && tick % 30 == 29)
                {
                    AppendTrace(trace, seconds, session.Server, trackers);
                }

                if (EnemiesAlive(session.Server) == 0)
                {
                    bossDead = true;
                    break;
                }

                if (!anyAlive)
                {
                    break;
                }
            }

            var results = new BalanceResult[trackers.Length];
            for (var i = 0; i < trackers.Length; i++)
            {
                results[i] = trackers[i].Finish(seconds, bossDead);
            }

            return results;
        }

        // One player's side of a run: the bot, what it has seen and what it has added up so far.
        sealed class Tracker
        {
            readonly GameClient client;
            readonly float maxHealth;
            readonly int ultimateId;
            readonly List<DamageEvent> events = new List<DamageEvent>();
            readonly List<UseEvent> uses = new List<UseEvent>();
            Vec2 last;
            bool hasLast;
            EntityState me;

            public Tracker(GameClient client, Registry<AbilityDef> abilities, Registry<ClassDef> classes, string weapon, string boss, IBotPolicy policy)
            {
                this.client = client;
                Policy = policy;
                View = new BotView(client, abilities, classes.Get(weapon)) { EnemyRadius = classes.Get(boss).BodyRadius, EnemyCasts = HasCasts(classes.Get(boss), abilities) };
                Result = new BalanceResult { Weapon = weapon, Boss = boss, Policy = policy.Name };
                maxHealth = classes.Get(weapon).Stats.MaxHealth;
                ultimateId = classes.Get(weapon).Skills.Length > Entity.UltimateSlot - 1 ? StableId.From(classes.Get(weapon).Skills[Entity.UltimateSlot - 1]) : 0;
            }

            public IBotPolicy Policy { get; }

            public BotView View { get; }

            static bool HasCasts(ClassDef boss, Registry<AbilityDef> abilities)
            {
                foreach (var id in boss.Skills)
                {
                    if (abilities.TryGet(id, out var skill) && skill.Interruptible)
                    {
                        return true;
                    }
                }

                return false;
            }

            public BalanceResult Result { get; }

            public bool Alive => client.TryGetLocalState(out var state) && state.Health > 0f;

            public void Observe()
            {
                events.Clear();
                client.DrainEvents(events);
                for (var i = 0; i < events.Count; i++)
                {
                    var hit = events[i];
                    if (hit.Outcome == HitOutcome.Interrupted && hit.Source == client.LocalEntity)
                    {
                        Result.Interrupts++;
                    }

                    if (hit.Outcome == HitOutcome.Healed || hit.Outcome == HitOutcome.Interrupted)
                    {
                        continue;
                    }

                    if (hit.Target == client.LocalEntity)
                    {
                        Result.DamageTaken += hit.Amount;
                    }
                    else if (hit.Source == client.LocalEntity)
                    {
                        Result.DamageDealt += hit.Amount;
                    }
                }

                uses.Clear();
                client.DrainUses(uses);
                for (var i = 0; i < uses.Count; i++)
                {
                    if (uses[i].User == client.LocalEntity && ultimateId != 0 && uses[i].Ability == ultimateId)
                    {
                        Result.UltimatesFired++;
                    }
                }

                if (client.TryGetLocalState(out me) && me.Health > 0f)
                {
                    if (hasLast)
                    {
                        Result.Moved += (me.Position - last).Length;
                    }

                    last = me.Position;
                    hasLast = true;
                }
            }

            public BalanceResult Finish(float seconds, bool bossDead)
            {
                Result.Seconds = seconds;
                Result.BossDead = bossDead;
                Result.Survived = client.TryGetLocalState(out me) && me.Health > 0f;
                Result.HealthLeft = Result.Survived && maxHealth > 0f ? me.Health / maxHealth : 0f;
                Result.MaxHealth = maxHealth;
                return Result;
            }
        }

        // One line a second for finding out why a bot moves: where everyone is and who the boss is after.
        static void AppendTrace(StringBuilder trace, float seconds, GameServer server, Tracker[] trackers)
        {
            trace.Append(seconds.ToString("0", CultureInfo.InvariantCulture)).Append("s");
            foreach (var entity in server.Entities)
            {
                if (!entity.IsAlive)
                {
                    continue;
                }

                var who = entity.Owner.IsValid ? "P" + entity.Owner.Value : entity.Team == Team.Enemies ? "E" + entity.ClassId : "?";
                var target = entity.Target.IsValid ? server.FindEntity(entity.Target) : null;
                trace.Append(string.Format(CultureInfo.InvariantCulture, "  {0}({1:0.0},{2:0.0}){3}", who, entity.Position.X, entity.Position.Y, target != null && target.Owner.IsValid ? "->P" + target.Owner.Value : string.Empty));
            }

            trace.AppendLine();
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

            // Played the intended way, a weapon should be hit now and then, or the boss asks nothing of it. Off (0) for now:
            // the bots react perfectly, and the playtest asked for calm fights with fewer attacks, so a perfect bot takes none
            // from rare attacks while a person still would. Set it above 0 to look for fights that are too safe.
            public float MinDamageTakenFraction { get; set; } = 0f;

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
            text.AppendLine("weapon              boss              policy           result     time  hp left  taken  dealt  ults  moved  ints");
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var outcome = r.BossDead && r.Survived ? "WIN" : r.Survived ? "timeout" : "DEAD";
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-19} {1,-17} {2,-16} {3,-8} {4,5:0.0}s {5,7:P0} {6,6:0} {7,6:0} {8,5:0} {9,5:0}m {10,5:0}",
                    r.Weapon, r.Boss, r.Policy, outcome, r.Seconds, r.HealthLeft, r.DamageTaken, r.DamageDealt, r.UltimatesFired, r.Moved, r.Interrupts));
            }

            return text.ToString();
        }
    }
}
