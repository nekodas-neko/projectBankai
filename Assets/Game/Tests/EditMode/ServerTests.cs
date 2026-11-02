using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ServerTests
    {
        const float Tick = 1f / 30f;

        [Test]
        public void AddPlayer_SpawnsEntityFromClassData()
        {
            var rig = new ServerRig(1);
            var entity = rig.Players[0];

            Assert.AreEqual(new PlayerId(1), entity.Owner);
            Assert.AreEqual(Team.Players, entity.Team);
            Assert.AreEqual(Role.Dps, entity.Role);
            Assert.AreEqual(100f, entity.Health);
            Assert.AreEqual("basic", entity.AbilityIds[0]);
            Assert.AreEqual("bolt", entity.AbilityIds[1]);
        }

        [Test]
        public void Move_AdvancesOneTickOfSpeed()
        {
            var rig = new ServerRig(1);
            rig.Send(0, 1, CommandType.Move, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(rig.Players[0].Position.X, -3f + 6f * Tick, 1e-4f);
        }

        [Test]
        public void Move_ClampsInputMagnitude()
        {
            var rig = new ServerRig(1);
            var start = rig.Players[0].Position;
            rig.Send(0, 1, CommandType.Move, new Vec2(50f, 0f));
            rig.Tick();

            Assert.AreEqual(6f * Tick, rig.Players[0].Position.X - start.X, 1e-4f);
        }

        [Test]
        public void Move_StaysInsideArena()
        {
            var rig = new ServerRig(1);
            for (var i = 1; i <= 30; i++)
            {
                rig.Send(0, i, CommandType.Move, new Vec2(1f, 0f));
                rig.Tick();
            }

            Assert.LessOrEqual(rig.Players[0].Position.Length, rig.Server.Simulation.ArenaRadius + 1e-4f);
        }

        [Test]
        public void StaleAndDuplicateSequences_AreIgnored()
        {
            var rig = new ServerRig(1);
            var start = rig.Players[0].Position;
            rig.Send(0, 5, CommandType.Move, new Vec2(1f, 0f));
            rig.Send(0, 5, CommandType.Move, new Vec2(1f, 0f));
            rig.Send(0, 3, CommandType.Move, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(6f * Tick, rig.Players[0].Position.X - start.X, 1e-4f);
        }

        [Test]
        public void CommandRate_IsCappedPerTick()
        {
            var rig = new ServerRig(1);
            var start = rig.Players[0].Position;
            for (var i = 1; i <= 6; i++)
            {
                rig.Send(0, i, CommandType.Move, new Vec2(1f, 0f));
            }

            rig.Tick();

            Assert.AreEqual(2 * 6f * Tick, rig.Players[0].Position.X - start.X, 1e-4f);
        }

        [Test]
        public void CommandQueue_DropsOverflow()
        {
            var config = new ServerConfig { MaxQueuedCommands = 4 };
            var rig = new ServerRig(1, config);
            var start = rig.Players[0].Position;
            for (var i = 1; i <= 20; i++)
            {
                rig.Send(0, i, CommandType.Move, new Vec2(1f, 0f));
            }

            rig.Tick(10);

            Assert.AreEqual(4 * 6f * Tick, rig.Players[0].Position.X - start.X, 1e-4f);
        }

        [Test]
        public void Commands_OnlyAffectTheirOwnConnection()
        {
            var rig = new ServerRig(2);
            var startOther = rig.Players[1].Position;
            rig.Send(0, 1, CommandType.Move, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(startOther, rig.Players[1].Position);
            Assert.AreNotEqual(rig.Players[0].Position, rig.Players[1].Position);
        }

        [Test]
        public void GarbageAndNonFinitePayloads_AreIgnored()
        {
            var rig = new ServerRig(1);
            var start = rig.Players[0].Position;
            rig.Links[0].ClientEnd.Send(new byte[] { 1, 2, 3 });
            rig.Links[0].ClientEnd.Send(new byte[0]);
            rig.Links[0].ClientEnd.Send(null);
            rig.Send(0, 1, CommandType.Move, new Vec2(float.NaN, 0f));
            rig.Send(0, 2, CommandType.Move, new Vec2(float.PositiveInfinity, 0f));
            rig.Tick(3);

            Assert.AreEqual(start, rig.Players[0].Position);
        }

        [Test]
        public void Attack_DamagesHostileInRangeAndStartsCooldown()
        {
            var rig = new ServerRig(1);
            var dummy = rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(2f, 0f));

            rig.Send(0, 1, CommandType.Attack, new Vec2(1f, 0f));
            rig.Send(0, 2, CommandType.Attack, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(90f, dummy.Health);
            Assert.Greater(rig.Players[0].Cooldowns[0], 0f);
        }

        [Test]
        public void Attack_WorksAgainAfterCooldown()
        {
            var rig = new ServerRig(1);
            var dummy = rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(2f, 0f));

            rig.Send(0, 1, CommandType.Attack, new Vec2(1f, 0f));
            rig.Tick(31);
            rig.Send(0, 2, CommandType.Attack, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(80f, dummy.Health);
        }

        [Test]
        public void AutoAim_PicksNearestHostileWithinRange()
        {
            var rig = new ServerRig(1);
            var origin = rig.Players[0].Position;
            var far = rig.Server.SpawnNpc("dummy", Team.Enemies, origin + new Vec2(0f, 2.5f));
            var near = rig.Server.SpawnNpc("dummy", Team.Enemies, origin + new Vec2(1f, 1f));

            rig.Send(0, 1, CommandType.Attack, Vec2.Zero);
            rig.Tick();

            Assert.AreEqual(90f, near.Health);
            Assert.AreEqual(100f, far.Health);
        }

        [Test]
        public void AutoAim_WithNoTarget_DoesNotSpendCooldown()
        {
            var rig = new ServerRig(1);

            rig.Send(0, 1, CommandType.Attack, Vec2.Zero);
            rig.Tick();

            Assert.AreEqual(0f, rig.Players[0].Cooldowns[0]);
        }

        [Test]
        public void ManualAim_MissesTargetsOffTheLine()
        {
            var rig = new ServerRig(1);
            var dummy = rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(2f, 2f));

            rig.Send(0, 1, CommandType.Skill1, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(100f, dummy.Health);
        }

        [Test]
        public void Skill_UsesSlotAbilityAndPower()
        {
            var rig = new ServerRig(1);
            rig.Players[0].Stats = new Stats { MaxHealth = 100f, MoveSpeed = 6f, Power = 2f };
            var dummy = rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(8f, 0f));

            rig.Send(0, 1, CommandType.Skill1, new Vec2(1f, 0f));
            rig.Tick();

            Assert.AreEqual(50f, dummy.Health);
        }

        [Test]
        public void EmptySkillSlot_IsRejected()
        {
            var rig = new ServerRig(1);
            rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(1f, 0f));

            rig.Send(0, 1, CommandType.Skill3, Vec2.Zero);
            rig.Tick();

            Assert.AreEqual(0f, rig.Players[0].Cooldowns[3]);
        }

        [Test]
        public void Attacks_DoNotHurtAllies()
        {
            var rig = new ServerRig(2);
            var ally = rig.Players[1];
            var direction = (ally.Position - rig.Players[0].Position).Normalized;

            rig.Send(0, 1, CommandType.Attack, direction);
            rig.Tick();

            Assert.AreEqual(100f, ally.Health);
        }

        [Test]
        public void DeadEntities_CannotActOrBeHit()
        {
            var rig = new ServerRig(1);
            var dummy = rig.Server.SpawnNpc("dummy", Team.Enemies, rig.Players[0].Position + new Vec2(1f, 0f));
            dummy.Health = 5f;

            rig.Send(0, 1, CommandType.Attack, new Vec2(1f, 0f));
            rig.Tick(31);
            Assert.AreEqual(0f, dummy.Health);

            rig.Send(0, 2, CommandType.Attack, new Vec2(1f, 0f));
            rig.Tick();
            Assert.AreEqual(0f, dummy.Health);

            rig.Players[0].Health = 0f;
            var position = rig.Players[0].Position;
            rig.Send(0, 3, CommandType.Move, new Vec2(1f, 0f));
            rig.Tick();
            Assert.AreEqual(position, rig.Players[0].Position);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void OneToFourPlayers_ShareTheSameCodePath(int count)
        {
            var rig = new ServerRig(count);
            var starts = new Vec2[count];
            for (var i = 0; i < count; i++)
            {
                starts[i] = rig.Players[i].Position;
                rig.Send(i, 1, CommandType.Move, new Vec2(0f, 1f));
            }

            rig.Tick();

            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(6f * Tick, rig.Players[i].Position.Y - starts[i].Y, 1e-4f, $"player {i}");
                Assert.AreEqual(starts[i].X, rig.Players[i].Position.X, 1e-4f);
            }
        }

        [Test]
        public void Update_RunsFixedTicksFromElapsedTime()
        {
            var rig = new ServerRig(1);

            rig.Server.Update(0.2f);

            Assert.AreEqual(6, rig.Server.Tick);
        }

        [Test]
        public void Update_CapsTicksAfterALongStall()
        {
            var rig = new ServerRig(1);

            rig.Server.Update(5f);

            Assert.AreEqual(8, rig.Server.Tick);
        }
    }
}
