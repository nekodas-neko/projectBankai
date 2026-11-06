using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TransportTests
    {
        static byte[] Payload(byte value) => new[] { value };

        [Test]
        public void NoConditions_DeliversImmediatelyInOrder()
        {
            var link = new LoopbackLink(new SeededRandom(1), NetworkConditions.None);
            link.ClientEnd.Send(Payload(1));
            link.ClientEnd.Send(Payload(2));

            Assert.IsTrue(link.ServerEnd.TryReceive(out var first));
            Assert.IsTrue(link.ServerEnd.TryReceive(out var second));
            Assert.IsFalse(link.ServerEnd.TryReceive(out _));
            Assert.AreEqual(1, first[0]);
            Assert.AreEqual(2, second[0]);
        }

        [Test]
        public void Latency_DelaysDeliveryUntilTimePasses()
        {
            var link = new LoopbackLink(new SeededRandom(1), new NetworkConditions { LatencySeconds = 0.1f });
            link.ClientEnd.Send(Payload(1));

            Assert.IsFalse(link.ServerEnd.TryReceive(out _));
            link.Advance(0.05f);
            Assert.IsFalse(link.ServerEnd.TryReceive(out _));
            link.Advance(0.06f);
            Assert.IsTrue(link.ServerEnd.TryReceive(out _));
        }

        [Test]
        public void Directions_AreIndependent()
        {
            var link = new LoopbackLink(new SeededRandom(1), NetworkConditions.None);
            link.ServerEnd.Send(Payload(9));

            Assert.IsFalse(link.ServerEnd.TryReceive(out _));
            Assert.IsTrue(link.ClientEnd.TryReceive(out var data));
            Assert.AreEqual(9, data[0]);
        }

        [Test]
        public void FullLoss_DropsEverything()
        {
            var link = new LoopbackLink(new SeededRandom(1), new NetworkConditions { LossRate = 1f });
            for (var i = 0; i < 20; i++)
            {
                link.ClientEnd.Send(Payload(1));
            }

            link.Advance(10f);

            Assert.IsFalse(link.ServerEnd.TryReceive(out _));
            Assert.AreEqual(20, link.PacketsDropped);
        }

        [Test]
        public void PartialLoss_IsReproducibleForASeed()
        {
            int Dropped(ulong seed)
            {
                var link = new LoopbackLink(new SeededRandom(seed), new NetworkConditions { LossRate = 0.3f });
                for (var i = 0; i < 1000; i++)
                {
                    link.ClientEnd.Send(Payload(1));
                }

                return link.PacketsDropped;
            }

            Assert.AreEqual(Dropped(5), Dropped(5));
            Assert.That(Dropped(5), Is.InRange(230, 370));
        }

        [Test]
        public void Jitter_StaysWithinBoundsAndCanReorder()
        {
            var link = new LoopbackLink(new SeededRandom(3), new NetworkConditions { LatencySeconds = 0.05f, JitterSeconds = 0.1f });
            for (byte i = 0; i < 50; i++)
            {
                link.ClientEnd.Send(Payload(i));
            }

            link.Advance(0.049f);
            Assert.IsFalse(link.ServerEnd.TryReceive(out _));

            link.Advance(0.2f);
            var received = 0;
            var reordered = false;
            var last = -1;
            while (link.ServerEnd.TryReceive(out var data))
            {
                reordered |= data[0] < last;
                last = data[0];
                received++;
            }

            Assert.AreEqual(50, received);
            Assert.IsTrue(reordered);
        }
    }
}
