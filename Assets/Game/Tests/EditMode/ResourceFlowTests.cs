using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ResourceFlowTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly Entity Player;
            readonly LoopbackLink link;
            int sequence;

            public Setup()
            {
                var abilities = TestData.Abilities();
                abilities.Add(new AbilityDef { Id = "gainer", Cooldown = 0.1f, Range = 3f, Radius = 0.5f, Damage = 1f, ResourceCost = -10f });
                abilities.Add(new AbilityDef { Id = "spender", Cooldown = 0.1f, Range = 3f, Radius = 0.5f, Damage = 5f, ResourceCost = 30f });
                var classes = TestData.Classes();
                classes.Add(new ClassDef
                {
                    Id = "ragefighter",
                    Playable = true,
                    Stats = new Stats { MaxHealth = 100f, MoveSpeed = 5f, Power = 1f },
                    AutoAttack = "gainer",
                    Skills = new[] { "spender" },
                    Resource = new ResourceDef { Id = "rage", Max = 100f, Start = 100f },
                });

                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), classes, abilities, random);
                link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), "ragefighter", link.ServerEnd);
                Server.SpawnNpc("dummy", Team.Enemies, Player.Position + new Vec2(0f, 1.5f));
            }

            public void Use(CommandType type)
            {
                TestData.Send(link.ClientEnd, ++sequence, type, new Vec2(0f, 1f));
                Server.StepTick();
                for (var i = 0; i < 4; i++)
                {
                    Server.StepTick();
                }
            }
        }

        [Test]
        public void TheSkillCanBeUsedThreeTimesInARowFromAFullBar()
        {
            var setup = new Setup();

            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);

            Assert.AreEqual(10f, setup.Player.Resource, 0.001f);
        }

        [Test]
        public void AFourthUseIsRefusedAndSpendsNothing()
        {
            var setup = new Setup();
            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);

            setup.Use(CommandType.Skill1);

            Assert.AreEqual(10f, setup.Player.Resource, 0.001f);
        }

        [Test]
        public void EachAutoAttackGivesTenBackSoTheSkillComesBackAfterTwo()
        {
            var setup = new Setup();
            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);
            setup.Use(CommandType.Skill1);

            setup.Use(CommandType.Attack);
            Assert.AreEqual(20f, setup.Player.Resource, 0.001f);
            setup.Use(CommandType.Skill1);
            Assert.AreEqual(20f, setup.Player.Resource, 0.001f);

            setup.Use(CommandType.Attack);
            Assert.AreEqual(30f, setup.Player.Resource, 0.001f);
            setup.Use(CommandType.Skill1);
            Assert.AreEqual(0f, setup.Player.Resource, 0.001f);
        }

        [Test]
        public void TheBarNeverGoesAboveItsMaximum()
        {
            var setup = new Setup();

            setup.Use(CommandType.Attack);
            setup.Use(CommandType.Attack);

            Assert.AreEqual(100f, setup.Player.Resource, 0.001f);
        }
    }
}
