using System.Collections.Generic;

namespace Game.Core.Tests
{
    static class TestData
    {
        public static Registry<AbilityDef> Abilities()
        {
            var registry = new Registry<AbilityDef>();
            registry.Add(new AbilityDef { Id = "basic", Cooldown = 1f, Range = 3f, Radius = 0.5f, Damage = 10f });
            registry.Add(new AbilityDef { Id = "bolt", Cooldown = 2f, Range = 10f, Radius = 0.5f, Damage = 25f });
            registry.Add(new AbilityDef { Id = "mend", Cooldown = 1f, Range = 8f, Radius = 1.5f, Heal = 20f });
            registry.Add(new AbilityDef { Id = "pulse", Cooldown = 1f, Range = 6f, Radius = 2f, Damage = 10f, Heal = 15f });
            registry.Add(new AbilityDef { Id = "provoke", Cooldown = 1f, Range = 5f, Radius = 2f, Damage = 5f, Taunt = 3f });
            registry.Add(new AbilityDef { Id = "nova", Shape = AbilityShape.Circle, Cooldown = 2f, Range = 4f, Radius = 4f, Damage = 10f, Windup = 0.3f });
            registry.Add(new AbilityDef { Id = "wave", Shape = AbilityShape.Cone, Cooldown = 2f, Range = 8f, Radius = 0f, Angle = 90f, Damage = 10f });
            registry.Add(new AbilityDef { Id = "jab", Cooldown = 0.1f, Range = 8f, Radius = 1f, Damage = 10f });
            registry.Add(new AbilityDef { Id = "heavy", Cooldown = 0.1f, Range = 8f, Radius = 1f, Damage = 10f, ResourceCost = 50f });
            registry.Add(new AbilityDef { Id = "smash", Cooldown = 2f, Range = 3f, Radius = 2f, Damage = 20f, Windup = 0.5f });
            registry.Add(new AbilityDef { Id = "counter", Cooldown = 0f, Range = 6f, Radius = 1f, Damage = 30f });
            registry.Add(new AbilityDef { Id = "dodge", Kind = AbilityKind.Dodge, Cooldown = 3f, Range = 1f, Duration = 0.5f, DashDistance = 1f, PerfectWindow = 0.15f, Rider = "counter" });
            registry.Add(new AbilityDef { Id = "guard", Kind = AbilityKind.Block, Cooldown = 4f, Range = 1f, Duration = 1f, Reduction = 0.6f });
            registry.Add(new AbilityDef { Id = "ward", Kind = AbilityKind.Shield, Cooldown = 5f, Range = 1f, Duration = 3f, Absorb = 30f });
            return registry;
        }

        public static Registry<ClassDef> Classes()
        {
            var registry = new Registry<ClassDef>();
            registry.Add(new ClassDef
            {
                Id = "fighter",
                Role = Role.Dps,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "bolt" },
            });
            registry.Add(new ClassDef
            {
                Id = "rogue",
                Role = Role.Dps,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 1f },
                AutoAttack = "basic",
                Defense = "dodge",
            });
            registry.Add(new ClassDef
            {
                Id = "knight",
                Role = Role.Tank,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Defense = "guard",
                Resource = new ResourceDef { Id = "rage", Max = 100f, GainTaken = 0.5f, GainBlocked = 1f },
            });
            registry.Add(new ClassDef
            {
                Id = "cleric",
                Role = Role.Healer,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Defense = "ward",
                Resource = new ResourceDef { Id = "mana", Max = 100f, GainBlocked = 0.5f },
            });
            registry.Add(new ClassDef
            {
                Id = "caster",
                Role = Role.Dps,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "jab",
                Skills = new[] { "heavy" },
                Resource = new ResourceDef { Id = "focus", Max = 100f, Start = 60f, GainDealt = 0.5f },
            });
            registry.Add(new ClassDef
            {
                Id = "priest",
                Role = Role.Healer,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "mend", "pulse" },
            });
            registry.Add(new ClassDef
            {
                Id = "warden",
                Role = Role.Tank,
                Stats = new Stats { MaxHealth = 200f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "provoke" },
                ThreatMultiplier = 3f,
            });
            registry.Add(new ClassDef
            {
                Id = "shaman",
                Role = Role.Dps,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "nova",
                Skills = new[] { "wave" },
            });
            registry.Add(new ClassDef
            {
                Id = "boss",
                Role = Role.None,
                Stats = new Stats { MaxHealth = 1000f, MoveSpeed = 3f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "nova", "wave" },
                AggroRange = 30f,
                Phases = new[]
                {
                    new PhaseDef { HealthBelow = 1f, Attacks = new[] { "basic" } },
                    new PhaseDef { HealthBelow = 0.6f, Attacks = new[] { "nova", "basic" }, SpawnClass = "dummy", SpawnCount = 2 },
                    new PhaseDef { HealthBelow = 0.3f, Attacks = new[] { "wave", "nova" }, SpeedScale = 2f, SpawnClass = "dummy", SpawnCount = 1 },
                },
            });
            registry.Add(new ClassDef
            {
                Id = "brute",
                Role = Role.None,
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 3f, Power = 1f },
                AutoAttack = "smash",
                AggroRange = 15f,
            });
            registry.Add(new ClassDef
            {
                Id = "dummy",
                Role = Role.None,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 0f, Power = 1f },
            });
            return registry;
        }

        public static LocalSession Session(int players, ulong seed = 1, NetworkConditions conditions = default, ServerConfig config = null)
        {
            var session = new LocalSession(config ?? new ServerConfig(), Classes(), Abilities(), seed, conditions);
            for (var i = 1; i <= players; i++)
            {
                session.AddPlayer(new PlayerId(i), "fighter");
            }

            return session;
        }

        public static void Send(ITransport transport, int sequence, CommandType type, Vec2 aim)
        {
            transport.Send(WireCodec.Encode(new Command(PlayerId.None, sequence, type, aim)));
        }

        public static IReadOnlyList<Entity> Players(GameServer server)
        {
            var result = new List<Entity>();
            foreach (var entity in server.Entities)
            {
                if (entity.Owner.IsValid)
                {
                    result.Add(entity);
                }
            }

            return result;
        }
    }

    sealed class ServerRig
    {
        public readonly GameServer Server;
        public readonly LoopbackLink[] Links;
        public readonly Entity[] Players;

        public ServerRig(int players, ServerConfig config = null)
        {
            var random = new SeededRandom(1);
            Server = new GameServer(config ?? new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            Links = new LoopbackLink[players];
            Players = new Entity[players];
            for (var i = 0; i < players; i++)
            {
                Links[i] = new LoopbackLink(random, NetworkConditions.None);
                Players[i] = Server.AddPlayer(new PlayerId(i + 1), "fighter", Links[i].ServerEnd);
            }
        }

        public void Send(int player, int sequence, CommandType type, Vec2 aim)
        {
            TestData.Send(Links[player].ClientEnd, sequence, type, aim);
        }

        public void Tick(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                Server.StepTick();
            }
        }
    }
}
