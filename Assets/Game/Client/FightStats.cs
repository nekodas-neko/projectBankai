using System.Collections.Generic;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // Adds up what each player did over the current run, from the damage events the client already receives.
    public sealed class FightStats
    {
        public sealed class Row
        {
            public PlayerId Owner;
            public float Dealt;
            public float Healed;
            public float Taken;
            public int Interrupts;
            public int Dodged;
            public int Blocked;
        }

        readonly Dictionary<EntityId, Row> rows = new Dictionary<EntityId, Row>();
        DungeonPhase lastPhase = DungeonPhase.None;

        public float Seconds { get; private set; }

        public IEnumerable<Row> Rows => rows.Values;

        public void Reset()
        {
            rows.Clear();
            Seconds = 0f;
        }

        // A new run (the dungeon went back to fighting after it ended) starts a fresh count.
        public void Step(Snapshot snapshot, float dt)
        {
            var phase = snapshot?.Dungeon ?? DungeonPhase.None;
            if (phase == DungeonPhase.Fighting && (lastPhase == DungeonPhase.Victory || lastPhase == DungeonPhase.Defeat || lastPhase == DungeonPhase.None))
            {
                Reset();
            }

            if (phase == DungeonPhase.Fighting || phase == DungeonPhase.Cleared)
            {
                Seconds += dt;
            }

            lastPhase = phase;
        }

        public void Add(DamageEvent hit, Snapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.TryFind(hit.Source, out var source);
            snapshot.TryFind(hit.Target, out var target);
            var sourcePlayer = source.Owner.IsValid;
            var targetPlayer = target.Owner.IsValid;
            switch (hit.Outcome)
            {
                case HitOutcome.Healed:
                    if (sourcePlayer)
                    {
                        RowFor(hit.Source, source).Healed += hit.Amount;
                    }

                    break;
                case HitOutcome.Interrupted:
                    if (sourcePlayer)
                    {
                        RowFor(hit.Source, source).Interrupts++;
                    }

                    break;
                default:
                    if (sourcePlayer && !targetPlayer)
                    {
                        RowFor(hit.Source, source).Dealt += hit.Amount;
                    }

                    if (targetPlayer && !sourcePlayer)
                    {
                        var row = RowFor(hit.Target, target);
                        row.Taken += hit.Amount;
                        row.Dodged += hit.Outcome == HitOutcome.Dodged ? 1 : 0;
                        row.Blocked += hit.Outcome == HitOutcome.Blocked ? 1 : 0;
                    }

                    break;
            }
        }

        Row RowFor(EntityId id, EntityState state)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                row = new Row { Owner = state.Owner };
                rows[id] = row;
            }

            return row;
        }

        // One line per player, highest damage first.
        public string Report(PlayerId local)
        {
            var list = new List<Row>(rows.Values);
            list.Sort((a, b) => b.Dealt.CompareTo(a.Dealt));
            var text = new StringBuilder();
            text.Append("Run time ").Append(Clock(Seconds));
            foreach (var row in list)
            {
                var perSecond = Seconds > 0.5f ? row.Dealt / Seconds : 0f;
                text.Append('\n').Append(row.Owner == local ? "You" : "Player " + row.Owner.Value)
                    .Append("   dealt ").Append(Whole(row.Dealt)).Append(" (").Append(Whole(perSecond)).Append("/s)")
                    .Append("   healed ").Append(Whole(row.Healed))
                    .Append("   taken ").Append(Whole(row.Taken));
                if (row.Blocked + row.Dodged + row.Interrupts > 0)
                {
                    text.Append("   blocks ").Append(row.Blocked).Append(" dodges ").Append(row.Dodged).Append(" stops ").Append(row.Interrupts);
                }
            }

            return text.ToString();
        }

        public static string Clock(float seconds)
        {
            var whole = (int)seconds;
            return $"{whole / 60}:{whole % 60:00}";
        }

        static string Whole(float value)
        {
            return ((int)(value + 0.5f)).ToString();
        }
    }
}
