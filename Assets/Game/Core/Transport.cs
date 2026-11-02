using System.Collections.Generic;

namespace Game.Core
{
    public interface ITransport
    {
        void Send(byte[] payload);

        bool TryReceive(out byte[] payload);
    }

    public struct NetworkConditions
    {
        public float LatencySeconds;
        public float JitterSeconds;
        public float LossRate;

        public static NetworkConditions None => default;
    }

    public sealed class LoopbackLink
    {
        readonly IRandom random;
        readonly Endpoint client;
        readonly Endpoint server;

        public LoopbackLink(IRandom random, NetworkConditions conditions)
        {
            this.random = random;
            Conditions = conditions;
            client = new Endpoint(this);
            server = new Endpoint(this);
            client.Peer = server;
            server.Peer = client;
        }

        public NetworkConditions Conditions { get; set; }

        public double Now { get; private set; }

        public ITransport ClientEnd => client;

        public ITransport ServerEnd => server;

        public int PacketsSent { get; private set; }

        public int PacketsDropped { get; private set; }

        public void Advance(float dt)
        {
            Now += dt;
        }

        void Deliver(Endpoint to, byte[] payload)
        {
            PacketsSent++;
            var conditions = Conditions;
            if (conditions.LossRate > 0f && random.NextFloat() < conditions.LossRate)
            {
                PacketsDropped++;
                return;
            }

            var delay = conditions.LatencySeconds + (conditions.JitterSeconds > 0f ? random.NextFloat() * conditions.JitterSeconds : 0f);
            to.Inbox.Add(new Packet { DeliverAt = Now + delay, Order = PacketsSent, Payload = payload });
        }

        struct Packet
        {
            public double DeliverAt;
            public int Order;
            public byte[] Payload;
        }

        sealed class Endpoint : ITransport
        {
            readonly LoopbackLink link;

            public Endpoint(LoopbackLink link)
            {
                this.link = link;
            }

            public Endpoint Peer;

            public readonly List<Packet> Inbox = new List<Packet>();

            public void Send(byte[] payload)
            {
                link.Deliver(Peer, payload);
            }

            public bool TryReceive(out byte[] payload)
            {
                var best = -1;
                for (var i = 0; i < Inbox.Count; i++)
                {
                    var packet = Inbox[i];
                    if (packet.DeliverAt > link.Now)
                    {
                        continue;
                    }

                    if (best < 0 || packet.DeliverAt < Inbox[best].DeliverAt
                        || (packet.DeliverAt == Inbox[best].DeliverAt && packet.Order < Inbox[best].Order))
                    {
                        best = i;
                    }
                }

                if (best < 0)
                {
                    payload = null;
                    return false;
                }

                payload = Inbox[best].Payload;
                Inbox.RemoveAt(best);
                return true;
            }
        }
    }
}
