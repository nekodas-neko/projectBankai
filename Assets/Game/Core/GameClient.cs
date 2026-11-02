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
        double accumulator;
        double secondsSinceSnapshot;
        double clock;
        int sequence;
        Vec2 moveInput;

        public GameClient(PlayerId player, ITransport transport, ClassDef classDef, ClientConfig config)
        {
            this.player = player;
            this.transport = transport;
            this.config = config;
            moveSpeed = classDef.Stats.MoveSpeed;
        }

        public PlayerId Player => player;

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

        public void Attack(Vec2 aim)
        {
            Send(CommandType.Attack, aim);
        }

        public void Skill(int slot, Vec2 aim)
        {
            if (slot < 1 || slot > ClassDef.SkillSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            Send((CommandType)((int)CommandType.Attack + slot), aim);
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

        void Send(CommandType type, Vec2 aim)
        {
            var safeAim = aim.IsFinite ? aim : Vec2.Zero;
            var command = new Command(player, ++sequence, type, safeAim);
            transport.Send(WireCodec.Encode(command));
        }

        void StepMove()
        {
            if (moveInput.SqrLength <= 1e-6f || !HasLocalEntity)
            {
                return;
            }

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
            events.AddRange(snapshot.Events);
            uses.AddRange(snapshot.Uses);

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
                return;
            }

            LocalEntity = own.Id;
            pendingMoves.RemoveAll(m => m.Command.Sequence <= snapshot.AckedSequence || clock - m.SentAt > config.PendingMoveTimeoutSeconds);

            var position = own.Position;
            for (var i = 0; i < pendingMoves.Count; i++)
            {
                position = Movement.Step(position, pendingMoves[i].Command.Aim, moveSpeed, config.Simulation);
            }

            PredictedPosition = position;
        }

        struct PendingMove
        {
            public Command Command;
            public double SentAt;
        }
    }
}
