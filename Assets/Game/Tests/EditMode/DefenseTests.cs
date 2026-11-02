using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DefenseTests
    {
        const int HitTick = 15;

        [Test]
        public void Windup_DelaysTheHitUntilItLands()
        {
            var arena = new Arena("fighter", 1);

            arena.Tick(HitTick - 1);
            Assert.AreEqual(100f, arena.Player.Health);

            arena.Tick(1);
            Assert.AreEqual(80f, arena.Player.Health, 0.001f);
        }

        [Test]
        public void Windup_IsCancelledWhenTheAttackerDies()
        {
            var arena = new Arena("fighter", 1);

            arena.Tick(3);
            arena.Npcs[0].Health = 0f;
            arena.Tick(HitTick);

            Assert.AreEqual(100f, arena.Player.Health);
        }

        [Test]
        public void Player_RespawnsAtFullHealthAfterTheDelay()
        {
            var arena = new Arena("cleric", 0);
            arena.Player.Health = 0f;
            arena.Player.Position = new Vec2(10f, 10f);

            arena.Tick(140);
            Assert.IsFalse(arena.Player.IsAlive);

            arena.Tick(20);
            Assert.AreEqual(100f, arena.Player.Health);
            Assert.AreEqual(-3f, arena.Player.Position.X, 0.001f);
            Assert.AreEqual(0f, arena.Player.Position.Y, 0.001f);
        }

        [Test]
        public void Npc_DoesNotRespawn()
        {
            var arena = new Arena("fighter", 0);
            var dummy = arena.AddNpc("dummy", new Vec2(5f, 5f));
            dummy.Health = 0f;

            arena.Tick(300);

            Assert.IsFalse(dummy.IsAlive);
        }

        [Test]
        public void Dodge_MakesTheHitMissAndMovesTheDodger()
        {
            var arena = new Arena("rogue", 1);
            var start = arena.Player.Position;

            arena.Tick(2);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(HitTick - 2);
            arena.Tick(20);

            Assert.AreEqual(100f, arena.Player.Health);
            Assert.AreEqual(start.X + 1f, arena.Player.Position.X, 0.1f);
            CollectionAssert.Contains(arena.Outcomes(), HitOutcome.Dodged);
        }

        [Test]
        public void Dodge_WithoutAimStepsBackFromFacing()
        {
            var arena = new Arena("rogue", 0);
            var start = arena.Player.Position;
            var facing = arena.Player.Facing;

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(20);

            Assert.Less(Vec2.Dot(arena.Player.Position - start, facing), -0.9f);
        }

        [Test]
        public void Dodge_ThatLandsRightBeforeTheHitFiresTheRider()
        {
            var arena = new Arena("rogue", 1);

            arena.Tick(11);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(HitTick - 11);

            Assert.AreEqual(100f, arena.Player.Health);
            Assert.AreEqual(470f, arena.Npcs[0].Health, 0.001f);
        }

        [Test]
        public void Dodge_ThatIsTooEarlyStillAvoidsTheHitButGivesNoRider()
        {
            var arena = new Arena("rogue", 1);

            arena.Tick(2);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(HitTick - 2);

            Assert.AreEqual(100f, arena.Player.Health);
            Assert.AreEqual(500f, arena.Npcs[0].Health, 0.001f);
        }

        [Test]
        public void Dodge_IgnoresMoveCommandsWhileDashing()
        {
            var arena = new Arena("rogue", 0);
            var start = arena.Player.Position;

            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(1);
            for (var i = 0; i < 5; i++)
            {
                arena.Send(CommandType.Move, new Vec2(0f, 1f));
                arena.Tick(1);
            }

            Assert.AreEqual(start.Y, arena.Player.Position.Y, 0.001f);
        }

        [Test]
        public void Dodge_PressedJustAfterTheHitIsForgivenAndFiresTheRider()
        {
            var arena = new Arena("rogue", 1);

            arena.Tick(HitTick + 1);
            Assert.AreEqual(80f, arena.Player.Health, 0.001f);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(1);

            Assert.AreEqual(100f, arena.Player.Health, 0.001f);
            Assert.AreEqual(470f, arena.Npcs[0].Health, 0.001f);
        }

        [Test]
        public void Dodge_PressedTooLateIsNotForgiven()
        {
            var arena = new Arena("rogue", 1);

            arena.Tick(HitTick + 12);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(1);

            Assert.AreEqual(80f, arena.Player.Health, 0.001f);
        }

        [Test]
        public void Block_PressedJustAfterTheHitRefundsTheBlockedShare()
        {
            var arena = new Arena("knight", 1);

            arena.Tick(HitTick + 1);
            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(1);

            Assert.AreEqual(92f, arena.Player.Health, 0.001f);
        }

        [Test]
        public void Shield_PressedJustAfterTheHitAbsorbsIt()
        {
            var arena = new Arena("cleric", 1);

            arena.Tick(HitTick + 1);
            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(1);

            Assert.AreEqual(100f, arena.Player.Health, 0.001f);
            Assert.AreEqual(10f, arena.Player.StatusMagnitude(StatusType.Shield), 0.001f);
        }

        [Test]
        public void Forgiveness_CannotBringBackADeadPlayer()
        {
            var arena = new Arena("rogue", 1);
            arena.Player.Health = 15f;

            arena.Tick(HitTick + 1);
            Assert.IsFalse(arena.Player.IsAlive);
            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(1);

            Assert.IsFalse(arena.Player.IsAlive);
        }

        [Test]
        public void Dodge_GoesOnCooldown()
        {
            var arena = new Arena("rogue", 0);

            arena.Send(CommandType.Defend, new Vec2(1f, 0f));
            arena.Tick(1);

            Assert.Greater(arena.Player.Cooldowns[Entity.DefenseSlot], 0f);
        }

        [Test]
        public void Block_ReducesDamageAndFeedsTheResource()
        {
            var arena = new Arena("knight", 1);

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(HitTick);

            Assert.AreEqual(92f, arena.Player.Health, 0.001f);
            Assert.AreEqual(12f + 4f, arena.Player.Resource, 0.001f);
            CollectionAssert.Contains(arena.Outcomes(), HitOutcome.Blocked);
        }

        [Test]
        public void Block_ExpiresAfterItsDuration()
        {
            var arena = new Arena("knight", 0);

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(1);
            Assert.IsTrue(arena.Player.HasStatus(StatusType.Blocking));

            arena.Tick(35);
            Assert.IsFalse(arena.Player.HasStatus(StatusType.Blocking));
        }

        [Test]
        public void Shield_AbsorbsDamageAndConvertsPartOfItToResource()
        {
            var arena = new Arena("cleric", 2);

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(HitTick);

            Assert.AreEqual(90f, arena.Player.Health, 0.001f);
            Assert.AreEqual(15f, arena.Player.Resource, 0.001f);
            Assert.IsFalse(arena.Player.HasStatus(StatusType.Shield));
        }

        [Test]
        public void Shield_FullyAbsorbingAHitReportsAbsorbed()
        {
            var arena = new Arena("cleric", 1);

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(HitTick);

            Assert.AreEqual(100f, arena.Player.Health);
            Assert.AreEqual(10f, arena.Player.Resource, 0.001f);
            CollectionAssert.Contains(arena.Outcomes(), HitOutcome.Absorbed);
        }

        [Test]
        public void Ability_NeedsEnoughResource()
        {
            var arena = new Arena("caster", 0);
            var target = arena.AddNpc("dummy", new Vec2(-3f, 4f));
            arena.Player.Facing = new Vec2(0f, 1f);

            arena.Send(CommandType.Skill1, new Vec2(0f, 1f));
            arena.Tick(1);
            Assert.AreEqual(90f, target.Health, 0.001f);
            Assert.AreEqual(15f, arena.Player.Resource, 0.001f);

            arena.Tick(6);
            arena.Send(CommandType.Skill1, new Vec2(0f, 1f));
            arena.Tick(1);
            Assert.AreEqual(90f, target.Health, 0.001f);
            Assert.AreEqual(15f, arena.Player.Resource, 0.001f);
        }

        [Test]
        public void Resource_GainsFromDamageDealt()
        {
            var arena = new Arena("caster", 0);
            arena.AddNpc("dummy", new Vec2(-3f, 4f));

            arena.Send(CommandType.Attack, new Vec2(0f, 1f));
            arena.Tick(1);

            Assert.AreEqual(65f, arena.Player.Resource, 0.001f);
        }

        [Test]
        public void Resource_RegeneratesAndDecaysWithinItsBounds()
        {
            var entity = Entity.FromClass(new EntityId(1), PlayerId.None, Team.Players, new ClassDef
            {
                Id = "x",
                Stats = new Stats { MaxHealth = 10f, Power = 1f },
                Resource = new ResourceDef { Id = "mana", Max = 20f, Start = 5f, Regen = 10f },
            }, Vec2.Zero);

            entity.TickTimers(1f);
            Assert.AreEqual(15f, entity.Resource, 0.001f);
            entity.TickTimers(1f);
            Assert.AreEqual(20f, entity.Resource, 0.001f);

            entity.ResourceSpec.Regen = -30f;
            entity.TickTimers(1f);
            Assert.AreEqual(0f, entity.Resource, 0.001f);
        }

        [Test]
        public void Npc_WithAggroChasesAndStopsInRange()
        {
            var arena = new Arena("fighter", 0);
            var brute = arena.AddNpc("brute", new Vec2(-3f, 10f));

            arena.Tick(90);

            var distance = (brute.Position - arena.Player.Position).Length;
            Assert.Less(distance, 3f);
            Assert.Greater(distance, 1f);
        }

        [Test]
        public void Npc_WithoutAggroStaysPut()
        {
            var arena = new Arena("fighter", 0);
            var dummy = arena.AddNpc("dummy", new Vec2(-3f, 4f));

            arena.Tick(60);

            Assert.AreEqual(new Vec2(-3f, 4f), dummy.Position);
        }

        [Test]
        public void Snapshot_CarriesResourceAndEffects()
        {
            var arena = new Arena("knight", 0);

            arena.Send(CommandType.Defend, Vec2.Zero);
            arena.Tick(1);
            var state = arena.LatestState();

            Assert.AreEqual(StableId.From("rage"), state.ResourceId);
            Assert.AreEqual(100f, state.MaxResource);
            Assert.AreNotEqual(0, state.Effects & EffectFlags.Blocking);
            Assert.Greater(state.GetCooldown(Entity.DefenseSlot), 0f);
        }

        [Test]
        public void Codec_RoundTripsTheNewFields()
        {
            var snapshot = new Snapshot { Tick = 5 };
            snapshot.Entities.Add(new EntityState
            {
                Id = new EntityId(1),
                Cooldown4 = 2.5f,
                Resource = 33f,
                MaxResource = 100f,
                ResourceId = 4242,
                Effects = EffectFlags.Invulnerable | EffectFlags.Dashing,
            });
            snapshot.Events.Add(new DamageEvent { Source = new EntityId(1), Target = new EntityId(2), Amount = 0f, Outcome = HitOutcome.Dodged });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(2.5f, decoded.Entities[0].GetCooldown(4));
            Assert.AreEqual(33f, decoded.Entities[0].Resource);
            Assert.AreEqual(4242, decoded.Entities[0].ResourceId);
            Assert.AreEqual(EffectFlags.Invulnerable | EffectFlags.Dashing, decoded.Entities[0].Effects);
            Assert.AreEqual(HitOutcome.Dodged, decoded.Events[0].Outcome);
        }

        [Test]
        public void Codec_RoundTripsTheDefendCommand()
        {
            var bytes = WireCodec.Encode(new Command(PlayerId.None, 9, CommandType.Defend, new Vec2(1f, 0f)));

            Assert.IsTrue(WireCodec.TryDecode(bytes, out Command decoded));
            Assert.AreEqual(CommandType.Defend, decoded.Type);
            Assert.AreEqual(Entity.DefenseSlot, decoded.AbilitySlot);
        }

        [Test]
        public void Validator_RejectsBrokenDefensesAndResources()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "bad_dodge", Kind = AbilityKind.Dodge, Range = 1f, Rider = "ghost" });
            abilities.Add(new AbilityDef { Id = "bad_block", Kind = AbilityKind.Block, Range = 1f, Duration = 1f, Reduction = 1.5f });
            abilities.Add(new AbilityDef { Id = "bad_ward", Kind = AbilityKind.Shield, Range = 1f });
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef
            {
                Id = "oops",
                Stats = new Stats { MaxHealth = 10f, Power = 1f },
                AutoAttack = "heavy",
                Defense = "basic",
                AggroRange = 5f,
                Resource = new ResourceDef { Id = "", Max = 0f, GainDealt = -1f },
            });
            classes.Add(new ClassDef
            {
                Id = "poor",
                Stats = new Stats { MaxHealth = 10f, Power = 1f },
                AutoAttack = "heavy",
            });

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, abilities));

            StringAssert.Contains("unknown rider 'ghost'", errors);
            StringAssert.Contains("'bad_dodge': a dodge needs a duration", errors);
            StringAssert.Contains("'bad_block': a block needs", errors);
            StringAssert.Contains("'bad_ward': a shield needs", errors);
            StringAssert.Contains("'oops': defense 'basic' must be", errors);
            StringAssert.Contains("'oops': resource needs an id", errors);
            StringAssert.Contains("'oops': resource max", errors);
            StringAssert.Contains("'oops': resource gains", errors);
            StringAssert.Contains("'poor': ability 'heavy' costs a resource", errors);
        }

        sealed class Arena
        {
            readonly SeededRandom random = new SeededRandom(1);
            readonly LoopbackLink link;
            readonly List<DamageEvent> events = new List<DamageEvent>();
            readonly List<Entity> npcs = new List<Entity>();
            int sequence;

            public Arena(string playerClass, int brutes)
            {
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), playerClass, link.ServerEnd);
                for (var i = 0; i < brutes; i++)
                {
                    AddNpc("brute", Player.Position + new Vec2(0f, 2f));
                }
            }

            public GameServer Server { get; }

            public Entity Player { get; }

            public IReadOnlyList<Entity> Npcs => npcs;

            public Entity AddNpc(string classId, Vec2 position)
            {
                var npc = Server.SpawnNpc(classId, Team.Enemies, position);
                npcs.Add(npc);
                return npc;
            }

            public void Send(CommandType type, Vec2 aim)
            {
                TestData.Send(link.ClientEnd, ++sequence, type, aim);
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public List<HitOutcome> Outcomes()
            {
                Drain();
                var result = new List<HitOutcome>();
                foreach (var ev in events)
                {
                    result.Add(ev.Outcome);
                }

                return result;
            }

            public EntityState LatestState()
            {
                var found = default(EntityState);
                while (link.ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot) && snapshot.TryFind(Player.Id, out var state))
                    {
                        found = state;
                        events.AddRange(snapshot.Events);
                    }
                }

                return found;
            }

            void Drain()
            {
                LatestState();
            }
        }
    }
}
