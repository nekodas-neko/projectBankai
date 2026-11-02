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
