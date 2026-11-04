using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    // The warden's spore cloud and the pools in his chamber (issue 70): everyone is hit, standing in the water takes most of it away.
    public class ShelterContentTests
    {
        static Registry<ClassDef> classes;
        static Registry<AbilityDef> abilities;
        static Registry<DungeonDef> dungeons;
        GameObject root;

        [OneTimeSetUp]
        public void Load()
        {
            DefinitionLoader.Load(out classes, out abilities, out dungeons);
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ShelterTest", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TheSporeCloudIsARedMapWideAttackThatWaterAlmostCancels()
        {
            var cloud = abilities.Get("spore_cloud");

            Assert.IsTrue(cloud.Everyone);
            Assert.IsFalse(cloud.Avoidable, "a block or a dodge does not answer it");
            Assert.GreaterOrEqual(cloud.Windup, 2.5f, "time to read the warning and walk to the water");
            Assert.AreEqual("water", cloud.ShelterTag);
            Assert.GreaterOrEqual(cloud.ShelterReduction, 0.8f);

            var warden = classes.Get("mushroom_warden");
            Assert.Contains("spore_cloud", warden.Skills);
            Assert.IsTrue(System.Array.Exists(warden.Phases[0].Mechanics, m => m.Ability == "spore_cloud"), "it comes on a timer");
        }

        [Test]
        public void TheCaveHasTwoPoolsInTheWardensChamberCloseEnoughToReachInTime()
        {
            var cave = dungeons.Get("slime_cave");
            var chamber = System.Array.Find(cave.Zones, z => z.Name == "Warden's chamber");
            var cloud = abilities.Get("spore_cloud");

            Assert.AreEqual(2, cave.Patches.Length);
            var slowest = Mathf.Min(classes.Get("sword_and_shield").Stats.MoveSpeed, classes.Get("staff").Stats.MoveSpeed, classes.Get("healing_mace").Stats.MoveSpeed);
            foreach (var patch in cave.Patches)
            {
                Assert.AreEqual("water", patch.Tag);
                Assert.GreaterOrEqual(patch.MinX, chamber.MinX);
                Assert.LessOrEqual(patch.MaxX, chamber.MaxX);
                Assert.GreaterOrEqual(patch.MinY, chamber.MinY);
                Assert.LessOrEqual(patch.MaxY, chamber.MaxY);

                // From the middle of the chamber (a fight is rarely at the wall) to the pool's nearest edge.
                var centre = new Vec2((chamber.MinX + chamber.MaxX) * 0.5f, (chamber.MinY + chamber.MaxY) * 0.5f);
                var nearestX = Mathf.Clamp(centre.X, patch.MinX, patch.MaxX);
                var nearestY = Mathf.Clamp(centre.Y, patch.MinY, patch.MaxY);
                var distance = (new Vec2(nearestX, nearestY) - centre).Length;
                Assert.Less(distance, slowest * (cloud.Windup - 0.5f), $"{patch.Name}: reachable from the middle of the chamber in the time left after reacting");
            }
        }

        [Test]
        public void ALivePlayerInTheWaterTakesAFractionAndOneOutsideTakesItAll()
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var wet = session.AddPlayer(new PlayerId(1), "sword_and_shield");
            var dry = session.AddPlayer(new PlayerId(2), "sword_and_shield");
            session.StartDungeon(dungeons.Get("slime_cave"));
            session.Server.FindPlayerEntity(new PlayerId(1)).Position = new Vec2(-7.5f, 24.5f);
            session.Server.FindPlayerEntity(new PlayerId(2)).Position = new Vec2(0f, 25f);
            session.Server.FindPlayerEntity(new PlayerId(1)).Position = new Vec2(-7.5f, 24.5f);
            var cloud = StableId.From("spore_cloud");
            var amounts = new Dictionary<int, float>();
            var lastTick = -1;

            for (var step = 0; step < 30 * 25 && amounts.Count < 2; step++)
            {
                session.Advance(1f / 30f);
                var snapshot = wet.Latest;
                if (snapshot == null || snapshot.Tick == lastTick)
                {
                    continue;
                }

                lastTick = snapshot.Tick;
                foreach (var hit in snapshot.Events)
                {
                    if (hit.Ability == cloud && hit.Outcome == HitOutcome.Hit)
                    {
                        amounts[hit.Target.Value] = hit.Amount;
                    }
                }
            }

            var wetId = session.Server.FindPlayerEntity(new PlayerId(1)).Id.Value;
            var dryId = session.Server.FindPlayerEntity(new PlayerId(2)).Id.Value;
            Assert.IsTrue(amounts.ContainsKey(wetId) && amounts.ContainsKey(dryId), "the cloud landed on both players within 25 seconds");
            Assert.AreEqual(18f, amounts[dryId], 0.6f);
            Assert.AreEqual(18f * 0.15f, amounts[wetId], 0.6f);
            Assert.IsNotNull(dry);
        }

        [Test]
        public void TheMapDrawsThePoolsAndFadesThemWithTheArea()
        {
            var map = new WorldMap(
                new[] { new ZoneDef { Name = "A", MinX = -10f, MinY = -10f, MaxX = 10f, MaxY = 10f }, new ZoneDef { Name = "B", MinX = -10f, MinY = 40f, MaxX = 10f, MaxY = 60f } },
                null,
                new[]
                {
                    new PatchDef { Name = "near", Tag = "water", MinX = 2f, MinY = 2f, MaxX = 6f, MaxY = 6f },
                    new PatchDef { Name = "far", Tag = "water", MinX = 2f, MinY = 45f, MaxX = 6f, MaxY = 50f },
                });
            var view = new MapView(root.transform, map, new Mesh(), null, null);

            Assert.AreEqual(2, view.PoolCount);
            var near = view.Root.Find("Patch near");
            var far = view.Root.Find("Patch far");
            view.Fade(0f, 0f);
            Assert.IsTrue(near.gameObject.activeSelf);
            Assert.IsFalse(far.gameObject.activeSelf, "a pool in a far room is not drawn");
        }

        [Test]
        public void TheWarningSaysWhereToStandAndTurnsCalmInTheWater()
        {
            var warning = ShelterWarning.Build((RectTransform)root.transform);
            Assert.IsFalse(warning.Visible);

            warning.Show("water", 1.8f, false);
            Assert.IsTrue(warning.Visible);
            Assert.AreEqual("Get to the water!  1.8", warning.Text);
            Assert.IsFalse(warning.Sheltered);

            warning.Show("water", 1.2f, true);
            Assert.AreEqual("Stay in the water  1.2", warning.Text);
            Assert.IsTrue(warning.Sheltered);

            warning.Hide();
            Assert.IsFalse(warning.Visible);
        }
    }
}
