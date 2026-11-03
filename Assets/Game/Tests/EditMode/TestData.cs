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
            registry.Add(new AbilityDef
            {
                Id = "spark",
                Cooldown = 2f,
                Range = 5f,
                Radius = 1f,
                Damage = 10f,
                ResourceCost = 10f,
                Variants = new[]
                {
                    new VariantDef { Id = "hot", DamageScale = 2f, RangeScale = 1.5f, CooldownScale = 0.5f, CostScale = 2f },
                    new VariantDef { Id = "cold", DamageScale = 0.5f },
                },
            });
            registry.Add(new AbilityDef { Id = "jab", Cooldown = 0.1f, Range = 8f, Radius = 1f, Damage = 10f });
            registry.Add(new AbilityDef { Id = "heavy", Cooldown = 0.1f, Range = 8f, Radius = 1f, Damage = 10f, ResourceCost = 50f });
            registry.Add(new AbilityDef { Id = "smash", Cooldown = 2f, Range = 3f, Radius = 2f, Damage = 20f, Windup = 0.5f });
            registry.Add(new AbilityDef { Id = "burst", Shape = AbilityShape.Circle, Cooldown = 1f, Range = 5f, Radius = 5f, Damage = 30f, ChargeRequired = 50f, ChargeFromDealt = 1f });
            registry.Add(new AbilityDef { Id = "bulwark_burst", Shape = AbilityShape.Circle, Cooldown = 1f, Range = 4f, Radius = 4f, Damage = 10f, ChargeRequired = 100f, ChargeFromTaken = 2f, ChargeFromBlocked = 3f });
            registry.Add(new AbilityDef { Id = "healing_burst", Shape = AbilityShape.Circle, Cooldown = 1f, Range = 5f, Radius = 5f, Heal = 20f, ChargeRequired = 40f, ChargeFromHealing = 2f });
            registry.Add(new AbilityDef { Id = "red_windup", Shape = AbilityShape.Circle, Cooldown = 3f, Range = 4f, Radius = 4f, Damage = 20f, Windup = 1.6f, Avoidable = false });
            registry.Add(new AbilityDef { Id = "bomb", Shape = AbilityShape.Circle, Cooldown = 0.1f, Range = 30f, Radius = 2f, Damage = 25f, Windup = 1f, Avoidable = false, AtTarget = true });
            registry.Add(new AbilityDef { Id = "red_slam", Shape = AbilityShape.Circle, Cooldown = 5f, Range = 3f, Radius = 3f, Damage = 20f, Avoidable = false });
            registry.Add(new AbilityDef { Id = "orange_slam", Shape = AbilityShape.Circle, Cooldown = 5f, Range = 3f, Radius = 3f, Damage = 20f });
            registry.Add(new AbilityDef { Id = "nibble", Shape = AbilityShape.Circle, Cooldown = 5f, Range = 2f, Radius = 2f, Damage = 0.1f });
            registry.Add(new AbilityDef { Id = "mend_cast", Shape = AbilityShape.Circle, Cooldown = 20f, Range = 6f, Radius = 0.01f, Heal = 60f, Windup = 1.5f, Interruptible = true });
            registry.Add(new AbilityDef { Id = "call_cast", Shape = AbilityShape.Circle, Cooldown = 20f, Range = 6f, Radius = 0.01f, Windup = 1f, Interruptible = true, SummonClass = "dummy", SummonSkin = "slime_green", SummonCount = 2 });
            registry.Add(new AbilityDef { Id = "kick", Shape = AbilityShape.Circle, Cooldown = 0.2f, Range = 3f, Radius = 3f, Damage = 1f, Interrupt = 2f });
            registry.Add(new AbilityDef { Id = "taunting_kick", Shape = AbilityShape.Circle, Cooldown = 0.2f, Range = 3f, Radius = 3f, Damage = 1f, Taunt = 5f });
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
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "bolt" },
            });
            registry.Add(new ClassDef
            {
                Id = "rogue",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 1f },
                AutoAttack = "basic",
                Defense = "dodge",
            });
            registry.Add(new ClassDef
            {
                Id = "knight",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Defense = "guard",
                Resource = new ResourceDef { Id = "rage", Max = 100f, GainTaken = 0.5f, GainBlocked = 1f },
            });
            registry.Add(new ClassDef
            {
                Id = "cleric",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Defense = "ward",
                Resource = new ResourceDef { Id = "mana", Max = 100f, GainBlocked = 0.5f },
            });
            registry.Add(new ClassDef
            {
                Id = "caster",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "jab",
                Skills = new[] { "heavy" },
                Resource = new ResourceDef { Id = "focus", Max = 100f, Start = 60f, GainDealt = 0.5f },
            });
            registry.Add(new ClassDef
            {
                Id = "priest",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "mend", "pulse" },
            });
            registry.Add(new ClassDef
            {
                Id = "warden",
                Playable = true,
                Stats = new Stats { MaxHealth = 200f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "provoke" },
                ThreatMultiplier = 3f,
            });
            registry.Add(new ClassDef
            {
                Id = "shaman",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "nova",
                Skills = new[] { "wave" },
            });
            registry.Add(new ClassDef
            {
                Id = "boss",
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
                Id = "giant",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 3f, Power = 1f },
                AutoAttack = "basic",
                AggroRange = 30f,
                BodyRadius = 1.8f,
            });
            registry.Add(new ClassDef
            {
                Id = "bulky",
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 0f, Power = 1f },
                BodyRadius = 0.5f,
            });
            registry.Add(new ClassDef
            {
                Id = "sparker",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "spark",
                Resource = new ResourceDef { Id = "focus", Max = 100f, Start = 100f },
            });
            registry.Add(new ClassDef
            {
                Id = "brute",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 3f, Power = 1f },
                AutoAttack = "smash",
                AggroRange = 15f,
            });
            registry.Add(new ClassDef
            {
                Id = "striker",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "jab", "burst" },
            });
            registry.Add(new ClassDef
            {
                Id = "bulwark",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "jab", "bulwark_burst" },
                Defense = "guard",
            });
            registry.Add(new ClassDef
            {
                Id = "chaplain",
                Playable = true,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "mend", "healing_burst" },
            });
            registry.Add(new ClassDef
            {
                Id = "red_brute",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "red_slam",
                AggroRange = 15f,
            });
            registry.Add(new ClassDef
            {
                Id = "orange_brute",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "orange_slam",
                AggroRange = 15f,
            });
            registry.Add(new ClassDef
            {
                Id = "red_warden",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "red_windup",
                AggroRange = 15f,
            });
            registry.Add(new ClassDef
            {
                Id = "bomber",
                Stats = new Stats { MaxHealth = 1000f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "basic",
                Skills = new[] { "bomb" },
                AggroRange = 30f,
                Phases = new[]
                {
                    new PhaseDef
                    {
                        HealthBelow = 1f,
                        Attacks = new[] { "basic" },
                        Mechanics = new[] { new MechanicDef { Ability = "bomb", IntervalSeconds = 4f, FirstDelaySeconds = 1f, Target = MechanicTarget.Farthest, Count = 1 } },
                    },
                },
            });
            registry.Add(new ClassDef
            {
                Id = "grunt",
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 4f, Power = 1f },
                AutoAttack = "basic",
                AggroRange = 8f,
                LeashRange = 14f,
            });
            registry.Add(new ClassDef
            {
                Id = "kicker",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 4f, Power = 1f },
                AutoAttack = "kick",
                BodyRadius = 0.5f,
            });
            registry.Add(new ClassDef
            {
                Id = "taunter",
                Stats = new Stats { MaxHealth = 500f, MoveSpeed = 4f, Power = 1f },
                AutoAttack = "taunting_kick",
                BodyRadius = 0.5f,
            });
            registry.Add(new ClassDef
            {
                Id = "mender",
                Stats = new Stats { MaxHealth = 300f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "nibble",
                Skills = new[] { "mend_cast" },
                AggroRange = 15f,
                BodyRadius = 1f,
                Phases = new[]
                {
                    new PhaseDef
                    {
                        Attacks = new[] { "nibble" },
                        Mechanics = new[] { new MechanicDef { Ability = "mend_cast", IntervalSeconds = 20f, FirstDelaySeconds = 0.5f, Target = MechanicTarget.Farthest } },
                    },
                },
            });
            registry.Add(new ClassDef
            {
                Id = "summoner",
                Stats = new Stats { MaxHealth = 300f, MoveSpeed = 0f, Power = 1f },
                AutoAttack = "nibble",
                Skills = new[] { "call_cast" },
                AggroRange = 15f,
                BodyRadius = 1f,
                Phases = new[]
                {
                    new PhaseDef
                    {
                        Attacks = new[] { "nibble" },
                        Mechanics = new[] { new MechanicDef { Ability = "call_cast", IntervalSeconds = 20f, FirstDelaySeconds = 0.5f, Target = MechanicTarget.Farthest } },
                    },
                },
            });
            registry.Add(new ClassDef
            {
                Id = "dummy",
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
