using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class RoleTests
    {
        [Test]
        public void Heal_RestoresAnAllyAndClampsToMaxHealth()
        {
            var room = new Room("priest", "fighter");
            var ally = room.Players[1];
            ally.Health = 50f;

            room.Send(0, CommandType.Skill1, new Vec2(1f, 0f));
            room.Tick(1);
            Assert.AreEqual(70f, ally.Health, 0.001f);

            ally.Health = 95f;
            room.Tick(40);
            room.Send(0, CommandType.Skill1, new Vec2(1f, 0f));
            room.Tick(1);
            Assert.AreEqual(100f, ally.Health, 0.001f);
            Assert.AreEqual(5f, room.LastHealAmount(), 0.001f);
        }

        [Test]
        public void Heal_AbilityWithNoDamageLeavesEnemiesAlone()
        {
            var room = new Room("priest", "fighter");
            var enemy = room.AddNpc("dummy", new Vec2(1f, 0f));

            room.Send(0, CommandType.Skill1, new Vec2(1f, 0f));
            room.Tick(1);

            Assert.AreEqual(100f, enemy.Health);
        }

        [Test]
        public void Heal_DoesNotReachADeadAlly()
        {
            var room = new Room("priest", "fighter");
            var ally = room.Players[1];
            ally.Health = 0f;

            room.Send(0, CommandType.Skill1, new Vec2(1f, 0f));
            room.Tick(1);

            Assert.IsFalse(ally.IsAlive);
        }

        [Test]
        public void MixedAbility_DamagesEnemiesAndHealsAlliesIncludingTheCaster()
        {
            var room = new Room("priest", "fighter");
            var enemy = room.AddNpc("dummy", new Vec2(1f, 0f));
            room.Players[0].Health = 80f;
            room.Players[1].Health = 50f;

            room.Send(0, CommandType.Skill2, new Vec2(1f, 0f));
            room.Tick(1);

            Assert.AreEqual(90f, enemy.Health, 0.001f);
            Assert.AreEqual(95f, room.Players[0].Health, 0.001f);
            Assert.AreEqual(65f, room.Players[1].Health, 0.001f);
        }

        [Test]
        public void Enemy_TargetsTheNearestPlayerWhenNobodyHasThreat()
        {
            var room = new Room("fighter", "warden");
            var brute = room.AddNpc("brute", new Vec2(-2.2f, 2f));

            room.Tick(1);

            Assert.Less(brute.Facing.X, 0f);
        }

        [Test]
        public void Enemy_SwitchesToWhoeverHasTheMostThreat()
        {
            var room = new Room("fighter", "warden");
            var brute = room.AddNpc("brute", new Vec2(-2.2f, 2f));

            room.Send(0, CommandType.Attack, new Vec2(0.8f, 2f));
            room.Send(1, CommandType.Attack, new Vec2(-1.2f, 2f));
            room.Tick(1);

            Assert.Greater(brute.ThreatOf(room.Players[1].Id), brute.ThreatOf(room.Players[0].Id));
            Assert.Greater(brute.Facing.X, 0f);
        }

        [Test]
        public void Taunt_ForcesTheTargetAndRaisesThreatAboveEveryone()
        {
            var room = new Room("fighter", "warden");
            var brute = room.AddNpc("brute", new Vec2(-2.2f, 2f));
            brute.AddThreat(room.Players[0].Id, 1000f);

            room.Send(1, CommandType.Skill1, new Vec2(-1.2f, 2f));
            room.Tick(1);

            Assert.Greater(brute.Facing.X, 0f);
            Assert.Greater(brute.ThreatOf(room.Players[1].Id), brute.ThreatOf(room.Players[0].Id));

            room.Tick(120);
            Assert.Greater(brute.ThreatOf(room.Players[1].Id), brute.ThreatOf(room.Players[0].Id));
        }

        [Test]
        public void Threat_IsIgnoredForDeadPlayers()
        {
            var room = new Room("fighter", "warden");
            var brute = room.AddNpc("brute", new Vec2(-2.2f, 2f));
            brute.AddThreat(room.Players[1].Id, 1000f);
            room.Players[1].Health = 0f;

            room.Tick(1);

            Assert.Less(brute.Facing.X, 0f);
        }

        [Test]
        public void Validator_RejectsNegativeHealTauntAndThreat()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "bad_heal", Range = 1f, Heal = -1f });
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef { Id = "bad", Stats = new Stats { MaxHealth = 10f, Power = 1f }, ThreatMultiplier = -1f });

            var errors = string.Join("\n", DefinitionValidator.Validate(classes, abilities));

            StringAssert.Contains("'bad_heal': heal and taunt", errors);
            StringAssert.Contains("'bad': threat multiplier", errors);
        }

        sealed class Room
        {
            readonly SeededRandom random = new SeededRandom(1);
            readonly List<LoopbackLink> links = new List<LoopbackLink>();
            readonly List<Entity> players = new List<Entity>();
            readonly List<DamageEvent> events = new List<DamageEvent>();
            readonly int[] sequences = new int[4];

            public Room(params string[] classes)
            {
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                for (var i = 0; i < classes.Length; i++)
                {
                    var link = new LoopbackLink(random, NetworkConditions.None);
                    links.Add(link);
                    players.Add(Server.AddPlayer(new PlayerId(i + 1), classes[i], link.ServerEnd));
                }
            }

            public GameServer Server { get; }

            public IReadOnlyList<Entity> Players => players;

            public Entity AddNpc(string classId, Vec2 position)
            {
                return Server.SpawnNpc(classId, Team.Enemies, position);
            }

            public void Send(int player, CommandType type, Vec2 aim)
            {
                TestData.Send(links[player].ClientEnd, ++sequences[player], type, aim);
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public float LastHealAmount()
            {
                var amount = 0f;
                while (links[0].ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot))
                    {
                        events.AddRange(snapshot.Events);
                    }
                }

                foreach (var ev in events)
                {
                    if (ev.Outcome == HitOutcome.Healed)
                    {
                        amount = ev.Amount;
                    }
                }

                return amount;
            }
        }
    }
}
