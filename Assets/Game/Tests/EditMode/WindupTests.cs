using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WindupTests
    {
        static readonly int Nova = StableId.From("nova");

        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Caster;
            int sequence;

            public Setup()
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Caster = Server.AddPlayer(new PlayerId(1), "shaman", Link.ServerEnd);
            }

            public void Cast()
            {
                TestData.Send(Link.ClientEnd, ++sequence, CommandType.Attack, new Vec2(0f, 1f));
                Server.StepTick();
            }

            public static Snapshot Latest(LoopbackLink link)
            {
                Snapshot latest = null;
                while (link.ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot))
                    {
                        latest = snapshot;
                    }
                }

                return latest;
            }
        }

        [Test]
        public void ASnapshotCarriesTheAttackWhileItWindsUp()
        {
            var setup = new Setup();

            setup.Cast();
            var snapshot = Setup.Latest(setup.Link);

            Assert.AreEqual(1, snapshot.Windups.Count);
            var windup = snapshot.Windups[0];
            Assert.AreEqual(setup.Caster.Id, windup.User);
            Assert.AreEqual(Nova, windup.Ability);
            Assert.AreEqual(setup.Caster.Position.X, windup.Origin.X, 0.001f);
            Assert.AreEqual(1f, windup.Direction.Y, 0.001f);
            Assert.AreEqual(0.3f - 1f / 30f, windup.Remaining, 0.01f);
        }

        [Test]
        public void TheRemainingTimeShrinksAndTheWindupEndsWhenTheAttackLands()
        {
            var setup = new Setup();
            setup.Cast();
            var first = Setup.Latest(setup.Link).Windups[0].Remaining;

            setup.Server.StepTick();
            var second = Setup.Latest(setup.Link).Windups[0].Remaining;
            Assert.Less(second, first);

            for (var i = 0; i < 12; i++)
            {
                setup.Server.StepTick();
            }

            Assert.AreEqual(0, Setup.Latest(setup.Link).Windups.Count);
        }

        [Test]
        public void APlayerJoiningMidWindupSeesTheRemainingTime()
        {
            var setup = new Setup();
            setup.Cast();
            setup.Server.StepTick();
            var lateLink = new LoopbackLink(new SeededRandom(2), NetworkConditions.None);
            setup.Server.AddPlayer(new PlayerId(2), "fighter", lateLink.ServerEnd);

            setup.Server.StepTick();
            var seen = Setup.Latest(lateLink);

            Assert.AreEqual(1, seen.Windups.Count);
            Assert.Less(seen.Windups[0].Remaining, 0.3f - 2f / 30f);
            Assert.Greater(seen.Windups[0].Remaining, 0f);
        }

        [Test]
        public void AnAttackerWhoDiesStopsShowingTheWarning()
        {
            var setup = new Setup();
            setup.Cast();
            setup.Caster.Health = 0f;

            setup.Server.StepTick();

            Assert.AreEqual(0, Setup.Latest(setup.Link).Windups.Count);
        }

        [Test]
        public void Codec_RoundTripsWindups()
        {
            var snapshot = new Snapshot { Tick = 2 };
            snapshot.Windups.Add(new WindupState { User = new EntityId(7), Ability = 11, Variant = 3, Origin = new Vec2(1f, 2f), Direction = new Vec2(0f, 1f), Remaining = 0.75f });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(1, decoded.Windups.Count);
            Assert.AreEqual(new EntityId(7), decoded.Windups[0].User);
            Assert.AreEqual(11, decoded.Windups[0].Ability);
            Assert.AreEqual(3, decoded.Windups[0].Variant);
            Assert.AreEqual(2f, decoded.Windups[0].Origin.Y);
            Assert.AreEqual(0.75f, decoded.Windups[0].Remaining);
        }

        [Test]
        public void Codec_RejectsATruncatedWindupList()
        {
            var snapshot = new Snapshot { Tick = 2 };
            snapshot.Windups.Add(new WindupState { User = new EntityId(7), Ability = 11 });
            var bytes = WireCodec.Encode(snapshot);

            var cut = new byte[bytes.Length - 3];
            System.Array.Copy(bytes, cut, cut.Length);

            Assert.IsFalse(WireCodec.TryDecode(cut, out Snapshot _));
        }
    }
}
