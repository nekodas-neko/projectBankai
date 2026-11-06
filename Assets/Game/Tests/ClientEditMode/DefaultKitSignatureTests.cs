using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // The small signature of each weapon's default defensive skill (issue 50): the sword and shield's block leaves a damage cut
    // behind, the mace's ward gives mana back, the staff's step heals a little.
    public class DefaultKitSignatureTests
    {
        static Registry<ClassDef> classes;
        static Registry<AbilityDef> abilities;

        [OneTimeSetUp]
        public void Load()
        {
            DefinitionLoader.Load(out classes, out abilities);
        }

        [Test]
        public void EachDefaultDefenceCarriesItsOwnSignatureAndTheOthersDoNot()
        {
            var block = abilities.Get(classes.Get("sword_and_shield").Defense);
            var ward = abilities.Get(classes.Get("healing_mace").Defense);
            var step = abilities.Get(classes.Get("staff").Defense);

            Assert.AreEqual(AbilityKind.Block, block.Kind);
            Assert.Greater(block.Fortify, 0f);
            Assert.Greater(block.FortifySeconds, block.Duration, "the cut outlasts the block");
            Assert.AreEqual(0f, block.SelfHeal);
            Assert.GreaterOrEqual(block.ResourceCost, 0f);

            Assert.AreEqual(AbilityKind.Shield, ward.Kind);
            Assert.Less(ward.ResourceCost, 0f, "a negative cost gives mana back");
            Assert.AreEqual(0f, ward.Fortify);
            Assert.AreEqual(0f, ward.SelfHeal);

            Assert.AreEqual(AbilityKind.Dodge, step.Kind);
            Assert.Greater(step.SelfHeal, 0f);
            Assert.Less(step.SelfHeal, classes.Get("staff").Stats.MaxHealth * 0.15f, "heals a little, not a lot");
            Assert.AreEqual(0f, step.Fortify);
        }

        [Test]
        public void TheMaceGetsManaBackTheStaffGetsHealthBackAndTheSwordKeepsACutAfterTheBlock()
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var mace = session.AddPlayer(new PlayerId(1), "healing_mace");
            var staff = session.AddPlayer(new PlayerId(2), "staff");
            var sword = session.AddPlayer(new PlayerId(3), "sword_and_shield");
            var maceEntity = session.Server.FindPlayerEntity(new PlayerId(1));
            var staffEntity = session.Server.FindPlayerEntity(new PlayerId(2));
            var swordEntity = session.Server.FindPlayerEntity(new PlayerId(3));
            maceEntity.Resource = 10f;
            staffEntity.Health = staffEntity.Stats.MaxHealth - 30f;
            var staffBefore = staffEntity.Health;

            mace.Defend(Vec2.Zero);
            staff.Defend(Vec2.Zero);
            sword.Defend(Vec2.Zero);
            for (var i = 0; i < 4; i++)
            {
                session.Advance(1f / 30f);
            }

            var cost = abilities.Get(classes.Get("healing_mace").Defense).ResourceCost;
            Assert.AreEqual(10f - cost, maceEntity.Resource, 0.5f, "the grant, plus a fraction of a second of the mace's own regeneration");
            Assert.AreEqual(staffBefore + abilities.Get(classes.Get("staff").Defense).SelfHeal * staffEntity.Stats.Power, staffEntity.Health, 0.01f, "the heal scales with power");
            Assert.IsTrue(swordEntity.HasStatus(StatusType.Blocking));
            Assert.IsTrue(swordEntity.HasStatus(StatusType.Fortified));

            var block = abilities.Get(classes.Get("sword_and_shield").Defense);
            for (var i = 0; i < (int)((block.Duration + 0.3f) * 30f); i++)
            {
                session.Advance(1f / 30f);
            }

            Assert.IsFalse(swordEntity.HasStatus(StatusType.Blocking), "the block is over");
            Assert.IsTrue(swordEntity.HasStatus(StatusType.Fortified), "the cut is not");
        }
    }
}
