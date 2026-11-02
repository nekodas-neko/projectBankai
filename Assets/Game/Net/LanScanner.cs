using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Game.Net
{
    public sealed class LanScanner : IDisposable
    {
        const float ProbeSeconds = 1f;
        const int MaxReceivesPerUpdate = 64;

        readonly Socket socket;
        readonly IPEndPoint explicitTarget;
        readonly int port;
        readonly List<Session> results = new List<Session>();
        readonly byte[] buffer = new byte[Protocol.MaxDatagram];
        double clock;
        double lastProbe = double.NegativeInfinity;

        public LanScanner(int port = Protocol.DefaultPort, IPAddress targetAddress = null)
        {
            this.port = port;
            explicitTarget = targetAddress != null ? new IPEndPoint(targetAddress, port) : null;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
            socket.EnableBroadcast = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        }

        public IReadOnlyList<Session> Results => results;

        public void Update(float dt)
        {
            clock += dt;
            if (clock - lastProbe >= ProbeSeconds)
            {
                lastProbe = clock;
                var probe = Protocol.Simple(Protocol.Discover);
                if (explicitTarget != null)
                {
                    Protocol.Send(socket, explicitTarget, probe);
                }
                else
                {
                    foreach (var address in BroadcastAddresses())
                    {
                        Protocol.Send(socket, new IPEndPoint(address, port), probe);
                    }
                }
            }

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

                if (Protocol.TryReadHeader(buffer, length, out var type) && type == Protocol.DiscoverReply)
                {
                    Record((IPEndPoint)from, length);
                }
            }
        }

        public void Dispose()
        {
            socket.Close();
        }

        public static List<IPAddress> BroadcastAddresses()
        {
            var addresses = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null)
                        {
                            continue;
                        }

                        var ip = unicast.Address.GetAddressBytes();
                        var mask = unicast.IPv4Mask.GetAddressBytes();
                        var directed = new byte[4];
                        for (var i = 0; i < 4; i++)
                        {
                            directed[i] = (byte)(ip[i] | ~mask[i]);
                        }

                        var broadcast = new IPAddress(directed);
                        if (!addresses.Contains(broadcast))
                        {
                            addresses.Add(broadcast);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return addresses;
        }

        void Record(IPEndPoint from, int length)
        {
            try
            {
                using var stream = new MemoryStream(buffer, Protocol.HeaderSize, length - Protocol.HeaderSize);
                using var reader = new BinaryReader(stream);
                var name = reader.ReadString();
                var players = reader.ReadByte();
                var max = reader.ReadByte();
                var id = reader.ReadInt32();
                if (!Protocol.ValidText(name, Protocol.MaxNameLength))
                {
                    return;
                }

                for (var i = 0; i < results.Count; i++)
                {
                    if (results[i].Id != id)
                    {
                        continue;
                    }

                    var better = IPAddress.IsLoopback(results[i].EndPoint.Address) && !IPAddress.IsLoopback(from.Address);
                    if (better || results[i].EndPoint.Equals(from))
                    {
                        results[i] = new Session(from, name, players, max, id);
                    }

                    return;
                }

                results.Add(new Session(from, name, players, max, id));
            }
            catch (Exception)
            {
            }
        }

        public readonly struct Session
        {
            public Session(IPEndPoint endPoint, string name, int players, int maxPlayers, int id)
            {
                Id = id;
                EndPoint = endPoint;
                Name = name;
                Players = players;
                MaxPlayers = maxPlayers;
            }

            public int Id { get; }

            public IPEndPoint EndPoint { get; }

            public string Name { get; }

            public int Players { get; }

            public int MaxPlayers { get; }
        }
    }
}
