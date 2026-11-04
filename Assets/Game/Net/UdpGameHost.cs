using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Game.Core;

namespace Game.Net
{
    public sealed class UdpGameHost : IDisposable
    {
        const float PeerTimeoutSeconds = 8f;
        const int MaxInbox = 64;
        const int MaxReceivesPerPoll = 256;

        readonly GameServer server;
        readonly Registry<ClassDef> classes;
        readonly Socket socket;
        readonly string hostName;
        readonly int maxRemotePlayers;
        readonly Dictionary<IPEndPoint, Peer> peers = new Dictionary<IPEndPoint, Peer>();
        readonly List<IPEndPoint> expired = new List<IPEndPoint>();
        readonly byte[] buffer = new byte[Protocol.MaxDatagram * 2];
        readonly int sessionId = new Random().Next();
        double clock;
        int nextPlayerId;

        public UdpGameHost(GameServer server, Registry<ClassDef> classes, int port, string hostName, int firstRemotePlayerId = 2, int maxPlayers = 4)
        {
            this.server = server;
            this.classes = classes;
            this.hostName = Protocol.ValidText(hostName, Protocol.MaxNameLength) ? hostName : "Host";
            maxRemotePlayers = Math.Max(0, maxPlayers - 1);
            nextPlayerId = firstRemotePlayerId;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            Port = ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        public int Port { get; }

        public int RemotePlayerCount => peers.Count;

        public int OversizeDrops { get; private set; }

        public void Poll(float dt)
        {
            clock += dt;
            for (var i = 0; i < MaxReceivesPerPoll; i++)
            {
                if (!socket.Poll(0, SelectMode.SelectRead))
                {
                    break;
                }

                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int length;
                try
                {
                    length = socket.ReceiveFrom(buffer, ref from);
                }
                catch (SocketException)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Handle((IPEndPoint)from, length);
            }

            expired.Clear();
            foreach (var pair in peers)
            {
                if (clock - pair.Value.LastSeen > PeerTimeoutSeconds)
                {
                    expired.Add(pair.Key);
                }
            }

            for (var i = 0; i < expired.Count; i++)
            {
                Remove(expired[i]);
            }
        }

        public void Dispose()
        {
            socket.Close();
            peers.Clear();
        }

        internal void SendData(IPEndPoint target, byte[] payload)
        {
            if (payload.Length + Protocol.HeaderSize > Protocol.MaxDatagram)
            {
                OversizeDrops++;
                return;
            }

            Protocol.Send(socket, target, Protocol.WrapData(payload));
        }

        void Handle(IPEndPoint from, int length)
        {
            if (!Protocol.TryReadHeader(buffer, length, out var type))
            {
                return;
            }

            switch (type)
            {
                case Protocol.Join:
                    HandleJoin(from, length);
                    break;
                case Protocol.Data:
                    if (peers.TryGetValue(from, out var peer))
                    {
                        peer.LastSeen = clock;
                        if (peer.Transport.Inbox.Count < MaxInbox)
                        {
                            var payload = new byte[length - Protocol.HeaderSize];
                            Buffer.BlockCopy(buffer, Protocol.HeaderSize, payload, 0, payload.Length);
                            peer.Transport.Inbox.Enqueue(payload);
                        }
                    }

                    break;
                case Protocol.Heartbeat:
                    if (peers.TryGetValue(from, out var alive))
                    {
                        alive.LastSeen = clock;
                    }

                    break;
                case Protocol.Leave:
                    Remove(from);
                    break;
                case Protocol.Discover:
                    ReplyToDiscovery(from);
                    break;
            }
        }

        void HandleJoin(IPEndPoint from, int length)
        {
            if (peers.TryGetValue(from, out var existing))
            {
                existing.LastSeen = clock;
                SendAccept(from, existing.Player);
                return;
            }

            string classId;
            int appearance;
            int[] variants;
            float power;
            float health;
            float speed;
            try
            {
                using var stream = new MemoryStream(buffer, Protocol.HeaderSize, length - Protocol.HeaderSize);
                using var reader = new BinaryReader(stream);
                classId = reader.ReadString();
                appearance = reader.ReadInt32();
                var name = reader.ReadString();
                int count = reader.ReadByte();
                if (count > Entity.AbilitySlots)
                {
                    Reject(from, Protocol.RejectBadRequest);
                    return;
                }

                variants = new int[count];
                for (var i = 0; i < count; i++)
                {
                    variants[i] = reader.ReadInt32();
                }

                power = reader.ReadUInt16() / 1000f;
                health = reader.ReadUInt16() / 1000f;
                speed = reader.ReadUInt16() / 1000f;
                if (!Protocol.ValidText(classId, Protocol.MaxClassLength) || !Protocol.ValidText(name, Protocol.MaxNameLength))
                {
                    Reject(from, Protocol.RejectBadRequest);
                    return;
                }
            }
            catch (Exception)
            {
                Reject(from, Protocol.RejectBadRequest);
                return;
            }

            if (peers.Count >= maxRemotePlayers)
            {
                Reject(from, Protocol.RejectFull);
                return;
            }

            if (!classes.TryGet(classId, out _))
            {
                Reject(from, Protocol.RejectUnknownClass);
                return;
            }

            var player = new PlayerId(nextPlayerId++);
            var transport = new PeerTransport(this, from);
            server.AddPlayer(player, classId, transport, appearance, variants, power, health, speed);
            peers.Add(from, new Peer { Player = player, Transport = transport, LastSeen = clock });
            SendAccept(from, player);
        }

        void SendAccept(IPEndPoint to, PlayerId player)
        {
            var writer = Protocol.Begin(Protocol.Accept);
            writer.Write(player.Value);
            writer.Write(server.Simulation.TickRate);
            writer.Write(server.Simulation.ArenaRadius);
            Protocol.Send(socket, to, Protocol.Finish(writer));
        }

        void Reject(IPEndPoint to, byte reason)
        {
            var writer = Protocol.Begin(Protocol.Reject);
            writer.Write(reason);
            Protocol.Send(socket, to, Protocol.Finish(writer));
        }

        void ReplyToDiscovery(IPEndPoint to)
        {
            var writer = Protocol.Begin(Protocol.DiscoverReply);
            writer.Write(hostName);
            writer.Write((byte)(peers.Count + 1));
            writer.Write((byte)(maxRemotePlayers + 1));
            writer.Write(sessionId);
            Protocol.Send(socket, to, Protocol.Finish(writer));
        }

        void Remove(IPEndPoint endpoint)
        {
            if (peers.TryGetValue(endpoint, out var peer))
            {
                server.RemovePlayer(peer.Player);
                peers.Remove(endpoint);
            }
        }

        sealed class Peer
        {
            public PlayerId Player;
            public PeerTransport Transport;
            public double LastSeen;
        }

        sealed class PeerTransport : ITransport
        {
            readonly UdpGameHost host;
            readonly IPEndPoint endpoint;

            public PeerTransport(UdpGameHost host, IPEndPoint endpoint)
            {
                this.host = host;
                this.endpoint = endpoint;
            }

            public readonly Queue<byte[]> Inbox = new Queue<byte[]>();

            public int MaxPayload => Protocol.MaxDatagram - Protocol.HeaderSize;

            public void Send(byte[] payload)
            {
                host.SendData(endpoint, payload);
            }

            public bool TryReceive(out byte[] payload)
            {
                if (Inbox.Count == 0)
                {
                    payload = null;
                    return false;
                }

                payload = Inbox.Dequeue();
                return true;
            }
        }
    }
}
