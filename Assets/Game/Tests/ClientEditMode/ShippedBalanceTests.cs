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

        // Every node of the weapon's tree taken at once, as the join message would carry it.
        static int[] FullTree(string weapon)
        {
            var trees = SkillTreeCatalog.Load(classes, abilities);
            var mastery = new WeaponMastery();
            foreach (var node in trees.Get(weapon).Nodes)
            {
                mastery.Choices.Add(node.Id);
            }

            return SkillTreeRules.Loadout(trees.Get(weapon), mastery);
        }

        // The skill trees are choices, not upgrades to chase: a fully taken tree may help, but it must not turn a boss into a short fight
        // or make the weapon unable to finish one it could finish with the default kit.
        [Test, Category("Balance")]
        public void AFullSkillTreeIsAnEdgeNotATrivialisation()
        {
            var table = new List<BalanceResult>();
            foreach (var boss in Bosses)
            {
                foreach (var weapon in Weapons)
                {
                    var policy = weapon == "sword_and_shield" ? (IBotPolicy)new TankBlocker() : new Kiter();
                    var plain = Play(weapon, boss, policy);
                    var treed = BalanceSim.Run(classes, abilities, weapon, boss, policy, 240f, 1, FullTree(weapon));
                    table.Add(plain);
                    table.Add(treed);

                    if (plain.BossDead)
                    {
                        Assert.IsTrue(treed.BossDead, $"{weapon} against {boss}: the full tree should not make the fight unwinnable" + BalanceSim.Table(new[] { plain, treed }));
                        Assert.GreaterOrEqual(treed.Seconds, plain.Seconds * 0.7f, $"{weapon} against {boss}: the full tree cuts the fight by more than 30%" + BalanceSim.Table(new[] { plain, treed }));
                    }
                }
            }

            var text = "Plain, then full tree, for each weapon and boss\n" + BalanceSim.Table(table);
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "tree-balance.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            TestContext.Progress.WriteLine(text);
        }

        // Loot varies a weapon's power inside the range of the shipped tables. The best drop must be an edge, not a runaway, and the
        // worst must still finish what the starter weapon finishes.
        [Test, Category("Balance")]
        public void TheBestAndWorstDropsStayCloseToTheStarterWeapon()
        {
            var modifiers = ModifierCatalog.Load();
            var tables = LootCatalog.Load(classes, modifiers);
            var best = 1f;
            var worst = 1f;
            foreach (var table in tables.All)
            {
                // The strongest drop is the top of the power range with the best power modifier the table can add.
                var bestModifier = 1f;
                if (table.ModifierChance > 0f)
                {
                    foreach (var id in table.Modifiers)
                    {
                        bestModifier = System.Math.Max(bestModifier, modifiers.Get(id).PowerScale);
                    }
                }

                best = System.Math.Max(best, table.PowerMax * bestModifier);
                worst = System.Math.Min(worst, table.PowerMin);
            }

            var rows = new List<BalanceResult>();
            foreach (var boss in Bosses)
            {
                foreach (var weapon in Weapons)
                {
                    var policy = weapon == "sword_and_shield" ? (IBotPolicy)new TankBlocker() : new Kiter();
                    var starter = Play(weapon, boss, policy);
                    var strong = BalanceSim.Run(classes, abilities, weapon, boss, policy, 240f, 1, null, best);
                    var weak = BalanceSim.Run(classes, abilities, weapon, boss, policy, 240f, 1, null, worst);
                    rows.Add(starter);
                    rows.Add(strong);
                    rows.Add(weak);

                    if (starter.BossDead)
                    {
                        Assert.IsTrue(strong.BossDead, $"{weapon} against {boss}: the best drop should still win" + BalanceSim.Table(new[] { starter, strong }));
                        // 32%: the warden's spore cloud costs a slow weapon a few seconds of damage each time (the walk to the water), so a faster
                        // kill avoids some clouds and a drop's edge on fight time is a little larger than its edge on damage.
                        Assert.GreaterOrEqual(strong.Seconds, starter.Seconds * 0.68f, $"{weapon} against {boss}: the best drop (x{best}) cuts the fight by more than 32%" + BalanceSim.Table(new[] { starter, strong }));
                        Assert.IsTrue(weak.BossDead || weak.Seconds >= 240f, $"{weapon} against {boss}: the worst drop (x{worst}) should not die where the starter wins" + BalanceSim.Table(new[] { starter, weak }));
                    }
                }
            }

            var text = $"Starter, then best drop (x{best}), then worst drop (x{worst}), for each weapon and boss" + System.Environment.NewLine + BalanceSim.Table(rows);
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "power-balance.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            TestContext.Progress.WriteLine(text);
        }

        // Duos and the full three: the dungeon is designed for a tank, a damage dealer and a healer.
        static IEnumerable<List<KeyValuePair<string, IBotPolicy>>> PartyGroups()
        {
            KeyValuePair<string, IBotPolicy> Tank() => new KeyValuePair<string, IBotPolicy>("sword_and_shield", new TankBlocker(false));
            KeyValuePair<string, IBotPolicy> Staff() => new KeyValuePair<string, IBotPolicy>("staff", new Kiter());
            KeyValuePair<string, IBotPolicy> Mace() => new KeyValuePair<string, IBotPolicy>("healing_mace", new Kiter());
            yield return new List<KeyValuePair<string, IBotPolicy>> { Tank(), Staff() };
            yield return new List<KeyValuePair<string, IBotPolicy>> { Tank(), Mace() };
            yield return new List<KeyValuePair<string, IBotPolicy>> { Tank(), Staff(), Mace() };
        }

        // What the shipped cave multiplies enemy health by for each extra player.
        static float PartyHealthPerExtraPlayer()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);
            return dungeons.Get("slime_cave").HealthPerExtraPlayer;
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
                foreach (var group in PartyGroups())
                {
                    party.AddRange(BalanceSim.RunParty(classes, abilities, group, boss, PartyHealthPerExtraPlayer()));
                }
            }

            var table = BalanceSim.Table(results) + "\nParty (a tank that never moves, ranged players holding back, boss health scaled by party size)\n" + BalanceSim.Table(party) + "\nTargets missed: " + misses.Count + "\n" + string.Join("\n", misses) + "\n";
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

        // A party must not trivialise a boss: with the cave's health scale a duo or a full party still takes most of the time a solo
        // player does (the bots are perfect, a person is slower), and everyone comes out with some health.
        [Test, Category("Balance")]
        public void ADuoOrAFullPartyDoesNotFinishABossInSeconds()
        {
            var shortest = new Dictionary<string, float> { ["mushroom_warden"] = 27f, ["brood_mother"] = 54f, ["slime_boss"] = 76f };
            foreach (var boss in Bosses)
            {
                foreach (var group in PartyGroups())
                {
                    var results = BalanceSim.RunParty(classes, abilities, group, boss, PartyHealthPerExtraPlayer());
                    var table = BalanceSim.Table(results);
                    Assert.IsTrue(results[0].BossDead, $"{boss}: the party did not finish it {table}");
                    Assert.GreaterOrEqual(results[0].Seconds, shortest[boss], $"{boss} fell too fast for a party of {results.Length} {table}");
                    foreach (var member in results)
                    {
                        Assert.GreaterOrEqual(member.HealthLeft, 0.3f, $"{member.Weapon} against {boss} in a party of {results.Length} {table}");
                    }
                }
            }
        }

        [Test, Category("Balance")]
        public void TheSwordAndTheStaffInterruptBossCasts()
        {
            // The mace's pulse can interrupt too, but only from within a few units of the boss, which the kiting bot never does.
            var sword = Play("sword_and_shield", "mushroom_warden", new TankBlocker());
            var staff = Play("staff", "mushroom_warden", new Kiter());

            Assert.Greater(sword.Interrupts, 0, BalanceSim.Table(new[] { sword }));
            Assert.Greater(staff.Interrupts, 0, BalanceSim.Table(new[] { staff }));
        }

        [Test, Category("Balance")]
        public void TheKingChargesACleaveThatTheSwordAndTheStaffCanBreak()
        {
            var sword = Play("sword_and_shield", "slime_boss", new TankBlocker());
            var staff = Play("staff", "slime_boss", new Kiter());

            Assert.Greater(sword.Interrupts, 0, BalanceSim.Table(new[] { sword }));
            Assert.Greater(staff.Interrupts, 0, BalanceSim.Table(new[] { staff }));
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
