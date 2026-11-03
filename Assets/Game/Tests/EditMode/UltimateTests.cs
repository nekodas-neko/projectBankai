using NUnit.Framework;

namespace Game.Core.Tests
{
    public class UltimateTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Player;
            int sequence;

            public Setup(string kit)
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), kit, Link.ServerEnd);
            }

            public void Send(CommandType type)
            {
                TestData.Send(Link.ClientEnd, ++sequence, type, new Vec2(0f, 1f));
                Server.StepTick();
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public Snapshot Latest()
            {
                Snapshot latest = null;
                while (Link.ClientEnd.TryReceive(out var data))
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
        public void TheUltimateStartsEmptyWithASizeFromTheAbility()
        {
            var setup = new Setup("striker");

            Assert.AreEqual(0f, setup.Player.Charge);
            Assert.AreEqual(50f, setup.Player.ChargeMax);
        }

        [Test]
        public void DamageDealtChargesTheDefaultUltimate()
        {
            var setup = new Setup("striker");
            setup.Server.SpawnNpc("dummy", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Attack);

            Assert.AreEqual(10f, setup.Player.Charge, 0.001f);
        }

        [Test]
        public void ChargeStopsAtTheMaximum()
        {
            var setup = new Setup("striker");
            setup.Player.Charge = 48f;
            setup.Server.SpawnNpc("dummy", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Attack);

            Assert.AreEqual(50f, setup.Player.Charge, 0.001f);
        }

        [Test]
        public void TheUltimateCannotFireBeforeItIsFull()
        {
            var setup = new Setup("striker");
            setup.Player.Charge = 49f;
            var target = setup.Server.SpawnNpc("dummy", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Skill2);

            Assert.AreEqual(100f, target.Health);
            Assert.AreEqual(49f, setup.Player.Charge);
            Assert.AreEqual(0f, setup.Player.Cooldowns[Entity.UltimateSlot]);
        }

        [Test]
        public void AFullUltimateFiresAndSpendsTheCharge()
        {
            var setup = new Setup("striker");
            setup.Player.Charge = 50f;
            var target = setup.Server.SpawnNpc("dummy", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Skill2);

            Assert.AreEqual(70f, target.Health, 0.001f);
            Assert.AreEqual(0f, setup.Player.Charge, 0.001f);
        }

        [Test]
        public void TheUltimateDoesNotChargeItself()
        {
            var setup = new Setup("striker");
            setup.Player.Charge = 50f;
            setup.Server.SpawnNpc("dummy", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Skill2);
            setup.Tick(3);

            Assert.AreEqual(0f, setup.Player.Charge, 0.001f);
        }

        [Test]
        public void DamageTakenChargesAnUltimateThatListensForIt()
        {
            var setup = new Setup("bulwark");
            var brute = setup.Server.SpawnNpc("brute", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Tick(30);

            var taken = 100f - setup.Player.Health;
            Assert.Greater(taken, 0f);
            Assert.AreEqual(taken * 2f, setup.Player.Charge, 0.01f);
            Assert.IsTrue(brute.IsAlive);
        }

        [Test]
        public void BlockedDamageChargesItMoreThanDamageTaken()
        {
            var setup = new Setup("bulwark");
            setup.Server.SpawnNpc("brute", Team.Enemies, setup.Player.Position + new Vec2(0f, 2f));

            setup.Send(CommandType.Defend);
            setup.Tick(30);

            var taken = 100f - setup.Player.Health;
            Assert.AreEqual(8f, taken, 0.01f);
            Assert.AreEqual(8f * 2f + 12f * 3f, setup.Player.Charge, 0.01f);
        }

        [Test]
        public void HealingDoneChargesAnUltimateThatListensForIt()
        {
            var setup = new Setup("chaplain");
            var link = new LoopbackLink(new SeededRandom(2), NetworkConditions.None);
            var ally = setup.Server.AddPlayer(new PlayerId(2), "striker", link.ServerEnd);
            ally.Position = setup.Player.Position + new Vec2(0f, 2f);
            ally.Health = 50f;

            setup.Send(CommandType.Skill1);

            Assert.AreEqual(70f, ally.Health, 0.001f);
            Assert.AreEqual(40f, setup.Player.Charge, 0.001f);
        }

        [Test]
        public void ASnapshotCarriesTheChargeAndItsSize()
        {
            var setup = new Setup("striker");
            setup.Player.Charge = 12f;

            setup.Tick(1);
            Assert.IsTrue(setup.Latest().TryFind(setup.Player.Id, out var state));

            Assert.AreEqual(12f, state.Charge);
            Assert.AreEqual(50f, state.ChargeMax);
        }

        [Test]
        public void ARestartEmptiesTheCharge()
        {
            var setup = new Setup("striker");
            setup.Server.StartDungeon(new DungeonDef { Id = "d", Rooms = new[] { new RoomDef { Spawns = new[] { new SpawnDef { Class = "dummy", X = 0f, Y = 10f } } } } });
            setup.Player.Charge = 30f;
            setup.Player.Health = 0f;
            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Defeat, setup.Server.DungeonPhase);

            setup.Send(CommandType.Restart);

            Assert.AreEqual(0f, setup.Player.Charge);
        }

        [Test]
        public void Validator_RejectsAnUltimateNothingCanCharge()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "dead_ultimate", Range = 1f, ChargeRequired = 100f });
            abilities.Add(new AbilityDef { Id = "negative_charge", Range = 1f, ChargeFromDealt = -1f });

            var errors = string.Join("\n", DefinitionValidator.Validate(TestData.Classes(), abilities));

            StringAssert.Contains("'dead_ultimate': needs charge but nothing charges it", errors);
            StringAssert.Contains("'negative_charge': charge numbers must not be negative", errors);
        }
    }
}
