using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CodecTests
    {
        [Test]
        public void Command_RoundTrips()
        {
            var original = new Command(PlayerId.None, 1234, CommandType.Skill2, new Vec2(0.25f, -0.75f));

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(original), out Command decoded));

            Assert.AreEqual(1234, decoded.Sequence);
            Assert.AreEqual(CommandType.Skill2, decoded.Type);
            Assert.AreEqual(new Vec2(0.25f, -0.75f), decoded.Aim);
            Assert.AreEqual(2, decoded.AbilitySlot);
        }

        [Test]
        public void Snapshot_RoundTrips()
        {
            var snapshot = new Snapshot { Tick = 99, AckedSequence = 7 };
            snapshot.Entities.Add(new EntityState
            {
                Id = new EntityId(3),
                Owner = new PlayerId(2),
                Team = Team.Players,
                Appearance = 123456,
                Position = new Vec2(1f, 2f),
                Facing = new Vec2(0f, 1f),
                Health = 42f,
                MaxHealth = 100f,
            });
            snapshot.Events.Add(new DamageEvent { Source = new EntityId(3), Target = new EntityId(4), Amount = 12.5f, Slot = 2, Ability = 777 });
            snapshot.Uses.Add(new UseEvent { User = new EntityId(3), Slot = 1, Ability = 888, Direction = new Vec2(0f, 1f) });

            var bytes = WireCodec.Encode(snapshot);
            Assert.IsTrue(WireCodec.TryDecode(bytes, out Snapshot decoded));

            Assert.AreEqual(99, decoded.Tick);
            Assert.AreEqual(7, decoded.AckedSequence);
            Assert.AreEqual(1, decoded.Entities.Count);
            Assert.AreEqual(42f, decoded.Entities[0].Health);
            Assert.AreEqual(12.5f, decoded.Events[0].Amount);
            Assert.AreEqual(123456, decoded.Entities[0].Appearance);
            Assert.AreEqual(777, decoded.Events[0].Ability);
            Assert.AreEqual(888, decoded.Uses[0].Ability);
            Assert.AreEqual(1, decoded.Uses.Count);
            Assert.AreEqual(1, decoded.Uses[0].Slot);
            Assert.AreEqual(new Vec2(0f, 1f), decoded.Uses[0].Direction);
            CollectionAssert.AreEqual(bytes, WireCodec.Encode(decoded));
        }

        [Test]
        public void Decode_RejectsTruncatedAndWrongTypeData()
        {
            var command = WireCodec.Encode(new Command(PlayerId.None, 1, CommandType.Move, new Vec2(1f, 0f)));
            var snapshot = WireCodec.Encode(new Snapshot { Tick = 1 });

            Assert.IsFalse(WireCodec.TryDecode(command[..5], out Command _));
            Assert.IsFalse(WireCodec.TryDecode(snapshot, out Command _));
            Assert.IsFalse(WireCodec.TryDecode(command, out Snapshot _));
            Assert.IsFalse(WireCodec.TryDecode(snapshot[..(snapshot.Length - 1)], out Snapshot _));
        }

        [Test]
        public void Decode_RejectsUnknownCommandType()
        {
            var bytes = WireCodec.Encode(new Command(PlayerId.None, 1, CommandType.Move, Vec2.Zero));
            bytes[5] = 99;

            Assert.IsFalse(WireCodec.TryDecode(bytes, out Command _));
        }

        [Test]
        public void Decode_SurvivesRandomBytes()
        {
            var random = new SeededRandom(7);
            for (var i = 0; i < 2000; i++)
            {
                var data = new byte[(int)(random.NextUInt() % 80)];
                for (var j = 0; j < data.Length; j++)
                {
                    data[j] = (byte)random.NextUInt();
                }

                if (data.Length > 0 && i % 2 == 0)
                {
                    data[0] = (byte)(1 + i % 2 + (i / 2) % 2);
                }

                Assert.DoesNotThrow(() =>
                {
                    WireCodec.TryDecode(data, out Command _);
                    WireCodec.TryDecode(data, out Snapshot _);
                });
            }
        }

        [Test]
        public void StableId_IsStableAndDistinct()
        {
            Assert.AreEqual(0, StableId.From(null));
            Assert.AreEqual(0, StableId.From(""));
            Assert.AreEqual(StableId.From("skin_ember"), StableId.From("skin_ember"));
            Assert.AreNotEqual(StableId.From("skin_ember"), StableId.From("skin_frost"));
            Assert.AreEqual(unchecked((int)0xE40C292C), StableId.From("a"));
        }

        [Test]
        public void SeededRandom_IsReproducible()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            var c = new SeededRandom(43);

            var sameAsB = true;
            var sameAsC = true;
            for (var i = 0; i < 100; i++)
            {
                var value = a.NextUInt();
                sameAsB &= value == b.NextUInt();
                sameAsC &= value == c.NextUInt();
            }

            Assert.IsTrue(sameAsB);
            Assert.IsFalse(sameAsC);
        }
    }
}
