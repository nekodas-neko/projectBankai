using Game.Core;
using NUnit.Framework;
using UnityEngine;
using EntityId = Game.Core.EntityId;

namespace Game.Client.Tests
{
    public class AggroRingsTests
    {
        static EntityState Idle()
        {
            return new EntityState { Id = new EntityId(5), Team = Team.Enemies, Health = 50f, AggroQuarters = 36 };
        }

        [Test]
        public void AnIdleEnemyWithARangeShowsARing()
        {
            Assert.IsTrue(AggroRings.ShouldShow(Idle()));
        }

        [Test]
        public void AnEnemyThatHasNoticedSomeoneShowsNoRing()
        {
            var entity = Idle();
            entity.Target = new EntityId(1);

            Assert.IsFalse(AggroRings.ShouldShow(entity));
        }

        [Test]
        public void DeadEnemiesPlayersAndThingsWithoutARangeShowNoRing()
        {
            var dead = Idle();
            dead.Health = 0f;
            var player = Idle();
            player.Team = Team.Players;
            var passive = Idle();
            passive.AggroQuarters = 0;

            Assert.IsFalse(AggroRings.ShouldShow(dead));
            Assert.IsFalse(AggroRings.ShouldShow(player));
            Assert.IsFalse(AggroRings.ShouldShow(passive));
        }

        [Test]
        public void TheRingIsABandAtTheAggroDistance()
        {
            var mesh = AggroRings.BuildRing(9f, 0.2f);

            var farthest = 0f;
            var nearest = float.MaxValue;
            foreach (var vertex in mesh.vertices)
            {
                farthest = Mathf.Max(farthest, vertex.magnitude);
                nearest = Mathf.Min(nearest, vertex.magnitude);
                Assert.AreEqual(0f, vertex.y, 0.0001f);
            }

            Assert.AreEqual(9f, farthest, 0.001f);
            Assert.AreEqual(8.8f, nearest, 0.001f);
            Assert.Greater(mesh.triangles.Length, 0);
            Object.DestroyImmediate(mesh);
        }
    }
}
