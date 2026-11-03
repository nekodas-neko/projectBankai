using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Game.Core;

namespace Game.Net
{
    public enum ConnectionState
    {
        Connecting,
        Connected,
        Rejected,
        Failed,
        Disconnected,
    }

    public sealed class UdpClientTransport : ITransport, IDisposable
    {
        const float JoinRetrySeconds = 0.4f;
        const int JoinAttempts = 25;
        const float HeartbeatSeconds = 1f;
        const float TimeoutSeconds = 8f;
        const int MaxInbox = 64;
        const int MaxReceivesPerUpdate = 256;

        readonly Socket socket;
        readonly IPEndPoint host;
        readonly byte[] joinPacket;
        readonly Queue<byte[]> inbox = new Queue<byte[]>();
        readonly byte[] buffer = new byte[Protocol.MaxDatagram * 2];
        double clock;
        double lastJoin = double.NegativeInfinity;
        double lastSent;
        double lastReceived;
        int attempts;

        public UdpClientTransport(IPEndPoint host, string classId, int appearance, string displayName, int[] variants = null, float weaponPower = 1f)
        {
            this.host = host;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            var writer = Protocol.Begin(Protocol.Join);
            writer.Write(classId);
            writer.Write(appearance);
            writer.Write(Protocol.ValidText(displayName, Protocol.MaxNameLength) ? displayName : "Player");
            var count = variants == null ? 0 : Math.Min(variants.Length, Entity.AbilitySlots);
            writer.Write((byte)count);
            for (var i = 0; i < count; i++)
            {
                writer.Write(variants[i]);
            }

            writer.Write(Protocol.PowerToWire(weaponPower));
            joinPacket = Protocol.Finish(writer);
        }

        public ConnectionState State { get; private set; } = ConnectionState.Connecting;

        public PlayerId PlayerId { get; private set; }

        public SimulationSettings Simulation { get; private set; }

        public byte RejectReason { get; private set; }

        public void Update(float dt)
        {
            if (State == ConnectionState.Rejected || State == ConnectionState.Failed || State == ConnectionState.Disconnected)
            {
                return;
            }

            clock += dt;
            Receive();

            if (State == ConnectionState.Connecting && clock - lastJoin >= JoinRetrySeconds)
            {
                if (attempts >= JoinAttempts)
                {
                    State = ConnectionState.Failed;
                    return;
                }

                attempts++;
                lastJoin = clock;
                Protocol.Send(socket, host, joinPacket);
            }
            else if (State == ConnectionState.Connected)
            {
                if (clock - lastReceived > TimeoutSeconds)
                {
                    State = ConnectionState.Disconnected;
                    return;
                }

                if (clock - lastSent >= HeartbeatSeconds)
                {
                    lastSent = clock;
                    Protocol.Send(socket, host, Protocol.Simple(Protocol.Heartbeat));
                }
            }
        }

        public int MaxPayload => Protocol.MaxDatagram - Protocol.HeaderSize;

        public void Send(byte[] payload)
        {
            if (State != ConnectionState.Connected || payload.Length + Protocol.HeaderSize > Protocol.MaxDatagram)
            {
                return;
            }

            lastSent = clock;
            Protocol.Send(socket, host, Protocol.WrapData(payload));
        }

        public bool TryReceive(out byte[] payload)
        {
            if (inbox.Count == 0)
            {
                payload = null;
                return false;
            }

            payload = inbox.Dequeue();
            return true;
        }

        public void Leave()
        {
            if (State == ConnectionState.Connected)
            {
                Protocol.Send(socket, host, Protocol.Simple(Protocol.Leave));
            }

            State = ConnectionState.Disconnected;
        }

        public void Dispose()
        {
            Leave();
            socket.Close();
        }

        void Receive()
        {
            for (var i = 0; i < MaxReceivesPerUpdate; i++)
            {
                if (!socket.Poll(0, SelectMode.SelectRead))
                {
                    return;
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

                if (!host.Equals(from) || !Protocol.TryReadHeader(buffer, length, out var type))
                {
                    continue;
                }

                lastReceived = clock;
                switch (type)
                {
                    case Protocol.Accept:
                        HandleAccept(length);
                        break;
                    case Protocol.Reject:
                        if (State == ConnectionState.Connecting)
                        {
                            RejectReason = length > Protocol.HeaderSize ? buffer[Protocol.HeaderSize] : Protocol.RejectBadRequest;
                            State = ConnectionState.Rejected;
                        }

                        break;
                    case Protocol.Data:
                        if (State == ConnectionState.Connected && inbox.Count < MaxInbox)
                        {
                            var payload = new byte[length - Protocol.HeaderSize];
                            Buffer.BlockCopy(buffer, Protocol.HeaderSize, payload, 0, payload.Length);
                            inbox.Enqueue(payload);
                        }

                        break;
                }
            }
        }

        void HandleAccept(int length)
        {
            if (State != ConnectionState.Connecting)
            {
                return;
            }

            try
            {
                using var stream = new MemoryStream(buffer, Protocol.HeaderSize, length - Protocol.HeaderSize);
                using var reader = new BinaryReader(stream);
                var player = reader.ReadInt32();
                var tickRate = reader.ReadInt32();
                var arenaRadius = reader.ReadSingle();
                if (player <= 0 || tickRate <= 0 || tickRate > 240 || !float.IsFinite(arenaRadius) || arenaRadius <= 0f)
                {
                    State = ConnectionState.Failed;
                    return;
                }

                PlayerId = new PlayerId(player);
                Simulation = new SimulationSettings(tickRate, arenaRadius);
                lastSent = clock;
                State = ConnectionState.Connected;
            }
            catch (Exception)
            {
                State = ConnectionState.Failed;
            }
        }
    }
}
