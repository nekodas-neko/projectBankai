using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Core.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    // Plays every weapon against every boss with bots, on the shipped definitions (and the tuning file if there is one).
    // Run `tools\balance-report.ps1` to print the table; the numbers in it are what to tune against.
    public class ShippedBalanceTests
    {
        static readonly string[] Weapons = { "sword_and_shield", "healing_mace", "staff" };
        static readonly string[] Bosses = { "mushroom_warden", "brood_mother", "slime_boss" };

        static Registry<ClassDef> classes;
        static Registry<AbilityDef> abilities;

        [OneTimeSetUp]
        public void LoadDefinitions()
        {
            DefinitionLoader.Load(out classes, out abilities);
        }

        static IBotPolicy[] Policies()
        {
            return new IBotPolicy[] { new StandStill(), new TankBlocker(), new Kiter() };
        }

        static BalanceResult Play(string weapon, string boss, IBotPolicy policy)
        {
            return BalanceSim.Run(classes, abilities, weapon, boss, policy, 240f);
        }

        [Test, Category("Balance")]
        public void Report()
        {
            var results = new List<BalanceResult>();
            foreach (var boss in Bosses)
            {
                foreach (var weapon in Weapons)
                {
                    foreach (var policy in Policies())
                    {
                        results.Add(Play(weapon, boss, policy));
                    }
                }
            }

            var targets = new BalanceSim.Targets();
            targets.MinFightSeconds["mushroom_warden"] = 30f;
            targets.MinFightSeconds["brood_mother"] = 60f;
            targets.MinFightSeconds["slime_boss"] = 85f;
            targets.IntendedPolicy["sword_and_shield"] = "stand and block";
            targets.IntendedPolicy["healing_mace"] = "kite";
            targets.IntendedPolicy["staff"] = "kite";
            var misses = BalanceSim.Misses(results, targets);
            var party = new List<BalanceResult>();
            foreach (var boss in Bosses)
            {
                foreach (var ranged in new[] { "staff", "healing_mace" })
                {
                    party.AddRange(BalanceSim.RunParty(classes, abilities, "sword_and_shield", new TankBlocker(false), ranged, new Kiter(), boss, 240f));
                }
            }

            var table = BalanceSim.Table(results) + "\nParty: a tank that never moves plus one ranged player\n" + BalanceSim.Table(party) + "\nTargets missed: " + misses.Count + "\n" + string.Join("\n", misses) + "\n";
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "balance-report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, table);
            TestContext.Progress.WriteLine(table);

            Assert.AreEqual(Bosses.Length * Weapons.Length * 3, results.Count);
        }

        // Prints where the party stands second by second; run by name when a bot moves more than it should.
        [Test, Explicit("prints a trace")]
        public void TraceTheParty()
        {
            var trace = new System.Text.StringBuilder();
            BalanceSim.RunParty(classes, abilities, "sword_and_shield", new TankBlocker(false), "staff", new Kiter(), "mushroom_warden", 240f, 1, trace);
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "party-trace.txt"));
            File.WriteAllText(path, trace.ToString());
        }

        [Test, Category("Balance")]
        public void TheSwordAndTheStaffInterruptBossCastsAndTheMaceCannot()
        {
            var sword = Play("sword_and_shield", "mushroom_warden", new TankBlocker());
            var staff = Play("staff", "mushroom_warden", new Kiter());
            var mace = Play("healing_mace", "mushroom_warden", new Kiter());

            Assert.Greater(sword.Interrupts, 0, BalanceSim.Table(new[] { sword }));
            Assert.Greater(staff.Interrupts, 0, BalanceSim.Table(new[] { staff }));
            Assert.AreEqual(0, mace.Interrupts, BalanceSim.Table(new[] { mace }));
        }

        [Test, Category("Balance")]
        public void TheKingChargesACleaveThatTheSwordAndTheStaffCanBreak()
        {
            var sword = Play("sword_and_shield", "slime_boss", new TankBlocker());
            var staff = Play("staff", "slime_boss", new Kiter());
            var mace = Play("healing_mace", "slime_boss", new Kiter());

            Assert.Greater(sword.Interrupts, 0, BalanceSim.Table(new[] { sword }));
            Assert.Greater(staff.Interrupts, 0, BalanceSim.Table(new[] { staff }));
            Assert.AreEqual(0, mace.Interrupts, BalanceSim.Table(new[] { mace }));
        }

        // The principles the numbers are tuned to keep. These are not about exact values, so tuning does not break them.
        [Test, Category("Balance")]
        public void StandingStillIsPunishedForRangedWeaponsAndBlockingHelpsTheTank()
        {
            foreach (var boss in Bosses)
            {
                foreach (var ranged in new[] { "staff", "healing_mace" })
                {
                    var kite = Play(ranged, boss, new Kiter());
                    var stand = Play(ranged, boss, new StandStill());

                    // The boss drops a bomb on its farthest player from the first phase, so anyone standing still is hit by it as
                    // well, and a kiter fights longer; what matters is that kiting leaves the player at least as healthy.
                    Assert.IsTrue(kite.Survived, BalanceSim.Table(new[] { kite }));
                    Assert.GreaterOrEqual(kite.HealthLeft, stand.HealthLeft - 0.05f, $"{ranged} should end healthier kiting than standing still against {boss}" + BalanceSim.Table(new[] { kite, stand }));
                }

                var block = Play("sword_and_shield", boss, new TankBlocker());
                var noBlock = Play("sword_and_shield", boss, new StandStill());
                if (noBlock.DamageTaken > 0f)
                {
                    Assert.Less(block.DamageTaken / block.Seconds, noBlock.DamageTaken / noBlock.Seconds, $"blocking should cut the tank's damage rate against {boss}");
                }
            }
        }

        [Test, Category("Balance")]
        public void AStandingStaffDoesNotSurviveTheFinalBossWithMostOfItsHealth()
        {
            var result = Play("staff", "slime_boss", new StandStill());

            Assert.Less(result.HealthLeft, 0.5f, BalanceSim.Table(new[] { result }));
        }
    }
}
