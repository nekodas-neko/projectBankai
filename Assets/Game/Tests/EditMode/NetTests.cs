using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Game.Net;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class NetTests
    {
        const float Step = 1f / 30f;

        GameServer server;
        UdpGameHost host;
        IPEndPoint endpoint;
        readonly System.Collections.Generic.List<IDisposable> disposables = new System.Collections.Generic.List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), new SeededRandom(1));
            host = new UdpGameHost(server, TestData.Classes(), 0, "Test host", maxPlayers: 3);
            endpoint = new IPEndPoint(IPAddress.Loopback, host.Port);
            disposables.Add(host);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            disposables.Clear();
        }

        UdpClientTransport Connect(string classId = "fighter", int appearance = 0)
        {
            var transport = new UdpClientTransport(endpoint, classId, appearance, "Tester");
            disposables.Add(transport);
            return transport;
        }

        void Pump(Func<bool> done, GameClient[] clients, UdpClientTransport[] transports, int milliseconds = 4000)
        {
            var deadline = Environment.TickCount + milliseconds;
            while (Environment.TickCount < deadline && !done())
            {
                host.Poll(Step);
                server.Update(Step);
                foreach (var transport in transports)
                {
                    transport.Update(Step);
                }

                foreach (var client in clients)
                {
                    client?.Update(Step);
                }

                Thread.Sleep(2);
            }
        }

        static GameClient MakeClient(UdpClientTransport transport)
        {
            return new GameClient(transport.PlayerId, transport, TestData.Classes().Get("fighter"), new ClientConfig { Simulation = transport.Simulation });
        }

        [Test]
        public void Join_CarriesTheVariantLoadout()
        {
            var hot = StableId.From("hot");
            var transport = new UdpClientTransport(endpoint, "sparker", 0, "Tester", new[] { hot });
            disposables.Add(transport);

            Pump(() => transport.State == ConnectionState.Connected, new GameClient[0], new[] { transport });

            Assert.AreEqual(ConnectionState.Connected, transport.State);
            Assert.AreEqual(hot, server.FindPlayerEntity(transport.PlayerId).Variants[0]);
        }

        [Test]
        public void TwoClients_JoinOverRealSocketsAndSeeEachOther()
        {
            var a = Connect(appearance: StableId.From("skin_a"));
            var b = Connect(appearance: StableId.From("skin_b"));
            var clients = new GameClient[2];
            Pump(() =>
            {
                clients[0] = clients[0] ?? (a.State == ConnectionState.Connected ? MakeClient(a) : null);
                clients[1] = clients[1] ?? (b.State == ConnectionState.Connected ? MakeClient(b) : null);
                return clients[0]?.Latest != null && clients[1]?.Latest != null && clients[0].Latest.Entities.Count == 2 && clients[1].Latest.Entities.Count == 2;
            }, clients, new[] { a, b });

            Assert.AreEqual(ConnectionState.Connected, a.State);
            Assert.AreEqual(ConnectionState.Connected, b.State);
            Assert.AreNotEqual(a.PlayerId, b.PlayerId);
            Assert.AreEqual(2, clients[0].Latest.Entities.Count);
            Assert.IsTrue(clients[1].Latest.TryFind(clients[0].LocalEntity, out var seen));
            Assert.AreEqual(StableId.From("skin_a"), seen.Appearance);
        }

        [Test]
        public void MovementAndPrediction_WorkOverRealSockets()
        {
            var transport = Connect();
            GameClient client = null;
            Pump(() =>
            {
                client = client ?? (transport.State == ConnectionState.Connected ? MakeClient(transport) : null);
                return client != null && client.HasLocalEntity;
            }, new GameClient[0], new[] { transport });
            Assert.IsNotNull(client);

            client.SetMoveInput(new Vec2(1f, 0f));
            var clients = new[] { client };
            for (var i = 0; i < 60; i++)
            {
                host.Poll(Step);
                server.Update(Step);
                transport.Update(Step);
                client.Update(Step);
                Thread.Sleep(2);
            }

            client.SetMoveInput(Vec2.Zero);
            Pump(() => false, clients, new[] { transport }, 600);

            var entity = server.FindPlayerEntity(transport.PlayerId);
            Assert.Greater(entity.Position.X, -3f + 1f);
            Assert.AreEqual(entity.Position.X, client.PredictedPosition.X, 0.05f);
        }

        [Test]
        public void UnknownClass_IsRejected()
        {
            var transport = Connect("not_a_class");
            Pump(() => transport.State != ConnectionState.Connecting, new GameClient[0], new[] { transport });

            Assert.AreEqual(ConnectionState.Rejected, transport.State);
            Assert.AreEqual(Protocol.RejectUnknownClass, transport.RejectReason);
            Assert.AreEqual(0, server.Entities.Count);
        }

        [Test]
        public void FullSession_RejectsAnotherPlayer()
        {
            var transports = new[] { Connect(), Connect(), Connect() };
            Pump(() =>
            {
                var connected = 0;
                var rejected = 0;
                foreach (var transport in transports)
                {
                    connected += transport.State == ConnectionState.Connected ? 1 : 0;
                    rejected += transport.State == ConnectionState.Rejected ? 1 : 0;
                }

                return connected + rejected == 3;
            }, new GameClient[0], transports);

            var accepted = 0;
            foreach (var transport in transports)
            {
                if (transport.State == ConnectionState.Connected)
                {
                    accepted++;
                }
                else
                {
                    Assert.AreEqual(Protocol.RejectFull, transport.RejectReason);
                }
            }

            Assert.AreEqual(2, accepted);
            Assert.AreEqual(2, server.Entities.Count);
        }

        [Test]
        public void Garbage_IsIgnoredAndDoesNotBreakTheHost()
        {
            using (var raw = new UdpClient())
            {
                var random = new SeededRandom(5);
                for (var i = 0; i < 200; i++)
                {
                    var bytes = new byte[random.NextUInt() % 80];
                    for (var j = 0; j < bytes.Length; j++)
                    {
                        bytes[j] = (byte)random.NextUInt();
                    }

                    raw.Send(bytes, bytes.Length, endpoint);
                }

                raw.Send(new byte[] { 0xB4, 0x4B, 1, Protocol.Join }, 4, endpoint);
                raw.Send(new byte[] { 0xB4, 0x4B, 1, Protocol.Data, 9, 9, 9 }, 7, endpoint);
            }

            for (var i = 0; i < 30; i++)
            {
                Assert.DoesNotThrow(() => host.Poll(Step));
                Thread.Sleep(2);
            }

            Assert.AreEqual(0, host.RemotePlayerCount);
            Assert.AreEqual(0, server.Entities.Count);
        }

        [Test]
        public void SilentClient_IsRemovedAfterTheTimeout()
        {
            var transport = Connect();
            Pump(() => transport.State == ConnectionState.Connected, new GameClient[0], new[] { transport });
            Assert.AreEqual(1, host.RemotePlayerCount);

            for (var i = 0; i < 10; i++)
            {
                host.Poll(1f);
            }

            Assert.AreEqual(0, host.RemotePlayerCount);
            Assert.AreEqual(0, server.Entities.Count);
        }

        [Test]
        public void LeavingClient_IsRemovedImmediately()
        {
            var transport = Connect();
            Pump(() => transport.State == ConnectionState.Connected, new GameClient[0], new[] { transport });

            transport.Leave();
            Pump(() => host.RemotePlayerCount == 0, new GameClient[0], new UdpClientTransport[0]);

            Assert.AreEqual(0, host.RemotePlayerCount);
        }

        [Test]
        public void Scanner_FindsAHostByName()
        {
            using (var scanner = new LanScanner(host.Port, IPAddress.Loopback))
            {
                var deadline = Environment.TickCount + 3000;
                while (Environment.TickCount < deadline && scanner.Results.Count == 0)
                {
                    scanner.Update(Step);
                    host.Poll(Step);
                    Thread.Sleep(2);
                }

                Assert.AreEqual(1, scanner.Results.Count);
                Assert.AreEqual("Test host", scanner.Results[0].Name);
                Assert.AreEqual(1, scanner.Results[0].Players);
                Assert.AreEqual(3, scanner.Results[0].MaxPlayers);
                Assert.AreEqual(host.Port, scanner.Results[0].EndPoint.Port);
            }
        }

        [Test]
        public void Scanner_ListsOneSessionEvenWhenItReplyComesFromTwoAddresses()
        {
            using (var first = new LanScanner(host.Port, IPAddress.Loopback))
            using (var second = new LanScanner(host.Port, IPAddress.Loopback))
            {
                var deadline = Environment.TickCount + 3000;
                while (Environment.TickCount < deadline && (first.Results.Count == 0 || second.Results.Count == 0))
                {
                    first.Update(Step);
                    second.Update(Step);
                    host.Poll(Step);
                    Thread.Sleep(2);
                }

                Assert.AreEqual(1, first.Results.Count);
                Assert.AreEqual(first.Results[0].Id, second.Results[0].Id);
                Assert.AreNotEqual(0, first.Results[0].Id);
            }
        }

        [TestCase("100.64.0.1", true)]
        [TestCase("100.101.102.103", true)]
        [TestCase("100.127.255.254", true)]
        [TestCase("100.128.0.1", false)]
        [TestCase("100.63.0.1", false)]
        [TestCase("192.168.1.20", false)]
        public void Tailscale_AddressesAreRecognised(string address, bool expected)
        {
            Assert.AreEqual(expected, LocalNetwork.IsTailscale(IPAddress.Parse(address)));
        }

        [Test]
        public void HostAddresses_AreListedWithThePort()
        {
            var lines = LocalNetwork.Addresses(47777);

            Assert.GreaterOrEqual(lines.Count, 1);
            foreach (var line in lines)
            {
                StringAssert.Contains(":47777", line);
            }
        }

        [Test]
        public void Scanner_ProbesEveryNetworkInterfaceNotJustTheDefault()
        {
            var addresses = LanScanner.BroadcastAddresses();

            Assert.Contains(IPAddress.Broadcast, addresses);
            Assert.GreaterOrEqual(addresses.Count, 1);
            foreach (var address in addresses)
            {
                Assert.AreEqual(AddressFamily.InterNetwork, address.AddressFamily);
            }
        }

        [Test]
        public void ConnectingToNobody_FailsInsteadOfHanging()
        {
            var nobody = new IPEndPoint(IPAddress.Loopback, 1);
            var transport = new UdpClientTransport(nobody, "fighter", 0, "Tester");
            disposables.Add(transport);

            for (var i = 0; i < 200 && transport.State == ConnectionState.Connecting; i++)
            {
                transport.Update(0.5f);
            }

            Assert.AreEqual(ConnectionState.Failed, transport.State);
        }
    }
}
