using System.Collections.Generic;

namespace Game.Core
{
    // The connected players: transports, command queues and sequence tracking.
    internal sealed class Session
    {
        readonly List<Connection> connections = new List<Connection>();
        readonly Snapshot fitted = new Snapshot();
        readonly List<EntityState> others = new List<EntityState>();
        readonly NearestFirst nearest = new NearestFirst();

        public int Count => connections.Count;

        public Connection this[int index] => connections[index];

        public Connection Find(PlayerId player)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].Player == player)
                {
                    return connections[i];
                }
            }

            return null;
        }

        public void Add(PlayerId player, ITransport transport, EntityId entity)
        {
            connections.Add(new Connection { Player = player, Transport = transport, Entity = entity });
        }

        public void Remove(Connection connection)
        {
            connections.Remove(connection);
        }

        public void Receive(Connection connection, int maxQueued)
        {
            while (connection.Transport.TryReceive(out var data))
            {
                if (!WireCodec.TryDecode(data, out Command command))
                {
                    continue;
                }

                if (connection.Queue.Count >= maxQueued)
                {
                    continue;
                }

                connection.Queue.Enqueue(command.WithPlayer(connection.Player));
            }
        }

        // Each connection gets the snapshot as is when it fits, and a version cut down to fit when it does not.
        public void Broadcast(Snapshot snapshot)
        {
            var fullSize = WireCodec.EncodedSize(snapshot);
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                var toSend = fullSize <= connection.Transport.MaxPayload ? snapshot : Fit(snapshot, connection, connection.Transport.MaxPayload);
                toSend.AckedSequence = connection.LastSequence;
                connection.Transport.Send(WireCodec.Encode(toSend));
            }
        }

        // Keeps every player, then as many of the other entities as fit, nearest to this connection's player first.
        // If even the players leave no room, the damage and use events go first (the next snapshot does not depend on them).
        Snapshot Fit(Snapshot full, Connection connection, int budget)
        {
            fitted.Tick = full.Tick;
            fitted.Room = full.Room;
            fitted.RoomCount = full.RoomCount;
            fitted.Dungeon = full.Dungeon;
            fitted.DungeonTimer = full.DungeonTimer;
            fitted.Entities.Clear();
            fitted.Events.Clear();
            fitted.Uses.Clear();
            fitted.Windups.Clear();
            fitted.Events.AddRange(full.Events);
            fitted.Uses.AddRange(full.Uses);
            fitted.Windups.AddRange(full.Windups);

            var origin = Vec2.Zero;
            var players = 0;
            others.Clear();
            for (var i = 0; i < full.Entities.Count; i++)
            {
                var entity = full.Entities[i];
                if (entity.Id == connection.Entity)
                {
                    origin = entity.Position;
                }

                if (entity.Owner.IsValid)
                {
                    fitted.Entities.Add(entity);
                    players++;
                }
                else
                {
                    others.Add(entity);
                }
            }

            if (WireCodec.EncodedSize(fitted) > budget)
            {
                fitted.Events.Clear();
                fitted.Uses.Clear();
            }

            nearest.Origin = origin;
            others.Sort(nearest);
            var room = budget - WireCodec.EncodedSize(fitted);
            for (var i = 0; i < others.Count && room >= WireCodec.EntityBytes; i++)
            {
                fitted.Entities.Add(others[i]);
                room -= WireCodec.EntityBytes;
            }

            return fitted;
        }

        sealed class NearestFirst : IComparer<EntityState>
        {
            public Vec2 Origin;

            public int Compare(EntityState a, EntityState b)
            {
                return (a.Position - Origin).SqrLength.CompareTo((b.Position - Origin).SqrLength);
            }
        }

        public sealed class Connection
        {
            public PlayerId Player;
            public ITransport Transport;
            public EntityId Entity;
            public int LastSequence;
            public readonly Queue<Command> Queue = new Queue<Command>();
        }
    }
}
