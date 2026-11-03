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
            targets.MinFightSeconds["slime_boss"] = 90f;
            targets.IntendedPolicy["sword_and_shield"] = "stand and block";
            targets.IntendedPolicy["healing_mace"] = "kite";
            targets.IntendedPolicy["staff"] = "kite";
            var misses = BalanceSim.Misses(results, targets);
            var table = BalanceSim.Table(results) + "\nTargets missed: " + misses.Count + "\n" + string.Join("\n", misses) + "\n";
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "balance-report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, table);
            TestContext.Progress.WriteLine(table);

            Assert.AreEqual(Bosses.Length * Weapons.Length * 3, results.Count);
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

                    Assert.Less(kite.DamageTaken, stand.DamageTaken, $"{ranged} should take less damage kiting than standing still against {boss}");
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
