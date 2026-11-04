using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ClientConfig
    {
        public SimulationSettings Simulation { get; set; } = SimulationSettings.Default;
        public float InterpolationDelay { get; set; } = 0.1f;
        public int MaxPendingMoves { get; set; } = 128;
        public double PendingMoveTimeoutSeconds { get; set; } = 1.0;
        public int SnapshotHistory { get; set; } = 32;
        public double PendingActionTimeoutSeconds { get; set; } = 1.0;
    }

    public sealed class GameClient
    {
        const double TickEpsilon = 1e-6;

        readonly PlayerId player;
        readonly ITransport transport;
        readonly ClientConfig config;
        readonly float moveSpeed;
        readonly List<PendingMove> pendingMoves = new List<PendingMove>();
        readonly List<Snapshot> history = new List<Snapshot>();
        readonly List<DamageEvent> events = new List<DamageEvent>();
        readonly List<UseEvent> uses = new List<UseEvent>();
        readonly List<PendingAction> pendingActions = new List<PendingAction>();
        readonly List<int> rejectedActions = new List<int>();
        double accumulator;
        double secondsSinceSnapshot;
        double clock;
        int sequence;
        Vec2 moveInput;
        Vec2 lastFacing = new Vec2(0f, 1f);
        Dash dash;

        public GameClient(PlayerId player, ITransport transport, ClassDef classDef, ClientConfig config)
        {
            this.player = player;
            this.transport = transport;
            this.config = config;
            moveSpeed = classDef.Stats.MoveSpeed;
        }

        public PlayerId Player => player;

        public WorldMap Map => config.Simulation.Map;

        public Snapshot Latest { get; private set; }

        public EntityId LocalEntity { get; private set; }

        public Vec2 PredictedPosition { get; private set; }

        public bool HasLocalEntity => LocalEntity.IsValid;

        public bool TryGetLocalState(out EntityState state)
        {
            state = default;
            return Latest != null && HasLocalEntity && Latest.TryFind(LocalEntity, out state);
        }

        public int PendingMoveCount => pendingMoves.Count;

        public void SetMoveInput(Vec2 input)
        {
            moveInput = input.IsFinite ? input.ClampLength(1f) : Vec2.Zero;
        }

        // Every command that does something returns its action id (the command's sequence). A caller that plays the effect at
        // once passes the id to ExpectEcho so the server's use event for it is not played a second time.
        public int Attack(Vec2 aim)
        {
            Remember(aim);
            return Send(CommandType.Attack, aim);
        }

        // The attack button held and let go: the heavy swing of the slot-0 attack.
        public int HeavyAttack(Vec2 aim)
        {
            Remember(aim);
            return Send(CommandType.HeavyAttack, aim);
        }

        public void UsePotion()
        {
            Send(CommandType.UsePotion, Vec2.Zero);
        }

        public int Skill(int slot, Vec2 aim)
        {
            if (slot < 1 || slot > ClassDef.SkillSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            Remember(aim);
            return Send((CommandType)((int)CommandType.Attack + slot), aim);
        }

        void Remember(Vec2 aim)
        {
            if (aim.IsFinite && aim.SqrLength > 1e-6f)
            {
                lastFacing = aim.Normalized;
            }
        }

        // The client played the effect of this action itself. When the server's use event for it arrives it is dropped instead
        // of being played again; if the server never produces one, the action is reported by DrainRejected.
        public void ExpectEcho(int actionId)
        {
            pendingActions.Add(new PendingAction { Id = actionId, SentAt = clock });
        }

        // Actions the server processed or timed out on without ever using them (a cooldown, no resource, dead).
        public void DrainRejected(List<int> into)
        {
            into.AddRange(rejectedActions);
            rejectedActions.Clear();
        }

        public bool IsDashing => dash.Active;

        public void Restart()
        {
            Send(CommandType.Restart, Vec2.Zero);
        }

        // A dodge. With a dash distance and time (the dodge ability's own numbers) the client also starts the dash at once and keeps
        // it going until the server has confirmed it, then settles on the server's position. A refused dodge snaps back.
        public int Defend(Vec2 direction, float dashDistance = 0f, float dashSeconds = 0f)
        {
            var aim = direction.IsFinite ? direction : Vec2.Zero;
            var id = Send(CommandType.Defend, aim);
            if (dashDistance > 0f && dashSeconds > 0f && HasLocalEntity)
            {
                var way = aim.SqrLength > 1e-6f ? aim.Normalized : lastFacing * -1f;
                dash = new Dash
                {
                    Active = true,
                    Sequence = id,
                    Direction = way,
                    Speed = dashDistance / dashSeconds,
                    Total = dashDistance,
                    Origin = PredictedPosition,
                    Remaining = dashSeconds,
                    SentAt = clock,
                };
            }

            return id;
        }

        public void Update(float dt)
        {
            accumulator += dt;
            secondsSinceSnapshot += dt;
            clock += dt;
            ReceiveSnapshots();

            var tickSeconds = (double)config.Simulation.TickSeconds;
            while (accumulator + TickEpsilon >= tickSeconds)
            {
                accumulator -= tickSeconds;
                StepMove();
            }
        }

        public void DrainEvents(List<DamageEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        public void DrainUses(List<UseEvent> into)
        {
            into.AddRange(uses);
            uses.Clear();
        }

        public bool TrySampleRemote(EntityId id, out Vec2 position)
        {
            position = default;
            if (history.Count == 0)
            {
                return false;
            }

            var tickSeconds = config.Simulation.TickSeconds;
            var renderTime = Latest.Tick * tickSeconds + secondsSinceSnapshot - config.InterpolationDelay;

            Snapshot before = null;
            Snapshot after = null;
            for (var i = 0; i < history.Count; i++)
            {
                var time = history[i].Tick * tickSeconds;
                if (time <= renderTime)
                {
                    before = history[i];
                }
                else
                {
                    after = history[i];
                    break;
                }
            }

            if (before == null)
            {
                before = after;
            }

            if (!before.TryFind(id, out var from))
            {
                return false;
            }

            if (after == null || !after.TryFind(id, out var to))
            {
                position = from.Position;
                return true;
            }

            var span = (after.Tick - before.Tick) * tickSeconds;
            var t = span > 0f ? (float)Math.Min(1.0, Math.Max(0.0, (renderTime - before.Tick * tickSeconds) / span)) : 1f;
            position = Vec2.Lerp(from.Position, to.Position, t);
            return true;
        }

        int Send(CommandType type, Vec2 aim)
        {
            var safeAim = aim.IsFinite ? aim : Vec2.Zero;
            var command = new Command(player, ++sequence, type, safeAim);
            transport.Send(WireCodec.Encode(command));
            return command.Sequence;
        }

        void StepMove()
        {
            if (!HasLocalEntity)
            {
                return;
            }

            if (dash.Remaining > 0f)
            {
                // The server ignores walking while a dodge runs, so the client does not walk either; it dashes.
                var tick = config.Simulation.TickSeconds;
                var step = Math.Min(dash.Remaining, tick);
                PredictedPosition = config.Simulation.Constrain(PredictedPosition + dash.Direction * (dash.Speed * step));
                dash.Remaining -= tick;
                return;
            }

            if (moveInput.SqrLength <= 1e-6f)
            {
                return;
            }

            lastFacing = moveInput.Normalized;

            var command = new Command(player, ++sequence, CommandType.Move, moveInput);
            transport.Send(WireCodec.Encode(command));

            if (pendingMoves.Count >= config.MaxPendingMoves)
            {
                pendingMoves.RemoveAt(0);
            }

            pendingMoves.Add(new PendingMove { Command = command, SentAt = clock });
            PredictedPosition = Movement.Step(PredictedPosition, command.Aim, moveSpeed, config.Simulation);
        }

        void ReceiveSnapshots()
        {
            while (transport.TryReceive(out var data))
            {
                if (WireCodec.TryDecode(data, out Snapshot snapshot))
                {
                    Apply(snapshot);
                }
            }
        }

        void Apply(Snapshot snapshot)
        {
            if (Latest != null && snapshot.Tick <= Latest.Tick)
            {
                return;
            }

            Latest = snapshot;
            secondsSinceSnapshot = 0.0;
            var map = config.Simulation.Map;
            if (map != null && !map.Authoritative && snapshot.Dungeon != DungeonPhase.None && snapshot.Room > 0)
            {
                map.Progress = snapshot.Room - 1;
            }
            events.AddRange(snapshot.Events);

            history.Add(snapshot);
            if (history.Count > config.SnapshotHistory)
            {
                history.RemoveAt(0);
            }

            var found = false;
            EntityState own = default;
            for (var i = 0; i < snapshot.Entities.Count; i++)
            {
                if (snapshot.Entities[i].Owner == player)
                {
                    own = snapshot.Entities[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                uses.AddRange(snapshot.Uses);
                return;
            }

            LocalEntity = own.Id;
            MatchEchoes(snapshot, own.Id);
            pendingMoves.RemoveAll(m => m.Command.Sequence <= snapshot.AckedSequence || clock - m.SentAt > config.PendingMoveTimeoutSeconds);

            if (ReconcileDash(snapshot, own))
            {
                return;
            }

            var position = own.Position;
            for (var i = 0; i < pendingMoves.Count; i++)
            {
                position = Movement.Step(position, pendingMoves[i].Command.Aim, moveSpeed, config.Simulation);
            }

            PredictedPosition = position;
        }

        // Uses the client already played are dropped; the rest go on to the caller. An action the server has processed without
        // using, or one that has gone unanswered too long, is rejected.
        void MatchEchoes(Snapshot snapshot, EntityId self)
        {
            for (var i = 0; i < snapshot.Uses.Count; i++)
            {
                var use = snapshot.Uses[i];
                var index = use.User == self && use.Sequence != 0 ? pendingActions.FindIndex(a => a.Id == use.Sequence) : -1;
                if (index >= 0)
                {
                    pendingActions.RemoveAt(index);
                    continue;
                }

                uses.Add(use);
            }

            for (var i = pendingActions.Count - 1; i >= 0; i--)
            {
                if (pendingActions[i].Id <= snapshot.AckedSequence || clock - pendingActions[i].SentAt > config.PendingActionTimeoutSeconds)
                {
                    rejectedActions.Add(pendingActions[i].Id);
                    pendingActions.RemoveAt(i);
                }
            }
        }

        // While a predicted dash is running or waiting for the server to catch up, snapshots do not move the client back to the
        // server's older position. It settles once the server has processed the dodge and is no longer dashing; a dodge the server
        // refused (it never moved) snaps back at once. Returns true while the position is left alone.
        bool ReconcileDash(Snapshot snapshot, in EntityState own)
        {
            if (!dash.Active)
            {
                return false;
            }

            var processed = snapshot.AckedSequence >= dash.Sequence;
            var serverDashing = (own.Effects & EffectFlags.Dashing) != 0;
            var moved = (own.Position - dash.Origin).Length;
            if (processed && !serverDashing && (moved < 0.2f || dash.Remaining <= 0f))
            {
                dash = default;
                return false;
            }

            if (clock - dash.SentAt > config.PendingActionTimeoutSeconds + dash.Total / Math.Max(dash.Speed, 0.01f))
            {
                dash = default;
                return false;
            }

            return true;
        }

        struct PendingAction
        {
            public int Id;
            public double SentAt;
        }

        struct Dash
        {
            public bool Active;
            public int Sequence;
            public Vec2 Direction;
            public float Speed;
            public float Total;
            public Vec2 Origin;
            public float Remaining;
            public double SentAt;
        }

        struct PendingMove
        {
            public Command Command;
            public double SentAt;
        }
    }
}
