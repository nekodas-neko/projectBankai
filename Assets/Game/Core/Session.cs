using System.Collections.Generic;

namespace Game.Core
{
    // The connected players: transports, command queues and sequence tracking.
    internal sealed class Session
    {
        readonly List<Connection> connections = new List<Connection>();

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

        public void Broadcast(Snapshot snapshot)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                snapshot.AckedSequence = connections[i].LastSequence;
                connections[i].Transport.Send(WireCodec.Encode(snapshot));
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
