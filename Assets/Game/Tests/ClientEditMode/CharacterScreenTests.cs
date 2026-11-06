using System.IO;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Tests
{
    public class CharacterScreenTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;
        GameObject root;
        MasteryProgress progress;
        Registry<ClassDef> classes;
        Registry<AbilityDef> abilities;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-character-" + System.Guid.NewGuid().ToString("N"));
            root = new GameObject("CharacterScreenTest", typeof(RectTransform));
            DefinitionLoader.Load(out classes, out abilities);
            var store = new ProfileStore(directory, Starter);
            var seed = store.Load();
            seed.Add(new OwnedWeapon { Uid = "staff-drop1", Type = "staff", Power = 1.1f, Modifier = "sturdy" });
            seed.AddArmor(new OwnedArmor { Uid = "stone_warden-drop1", Set = "stone_warden" });
            seed.AddGold(140);
            seed.Equip("staff-drop1");
            seed.EquipArmor("stone_warden-drop1");
            store.Save(seed);
            progress = new MasteryProgress(store, new MasteryRules());
            progress.UseModifiers(ModifierCatalog.Load());
            progress.UseArmorSets(ArmorCatalog.Load());
        }

        readonly System.Collections.Generic.List<CharacterScreen> screens = new System.Collections.Generic.List<CharacterScreen>();

        CharacterScreen NewScreen(System.Action<CharacterScreen.Tab, string> equip, System.Action<CharacterScreen.Tab, string> takeApart, System.Action changed)
        {
            var screen = new CharacterScreen((RectTransform)root.transform, equip, takeApart, changed, () => { });
            screens.Add(screen);
            return screen;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var screen in screens)
            {
                screen.Dispose();
            }

            Object.DestroyImmediate(root);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void TheInfoShowsTheWeaponTheArmorTheNumbersAndTheSkills()
        {
            var info = CharacterInfo.Build(progress, classes.Get("staff"), abilities, new int[Entity.AbilitySlots]);

            StringAssert.StartsWith("Staff +10% (Sturdy)", info.Weapon);
            Assert.AreEqual("Armor: Stone Warden (health +10%)", info.Armor);
            StringAssert.Contains("Health 97", info.Stats, "the staff's 80 health, plus 10% for the modifier and 10% for the armor");
            Assert.AreEqual("Gold 140   Potions 0/5", info.Wallet);
            StringAssert.Contains("Attack: Arcane Bolt", info.Skills);
            StringAssert.Contains("Defence: Arcane Step", info.Skills);
        }

        [Test]
        public void TheSheetShowsTheNumbersAsBarsAndTheSkillsAsIcons()
        {
            var loadout = new int[Entity.AbilitySlots];
            loadout[1] = StableId.From("frost");
            var screen = NewScreen((_, __) => { }, (_, __) => { }, () => { });

            screen.Show(CharacterInfo.Build(progress, classes.Get("staff"), abilities, loadout), progress);

            Assert.AreEqual("97", screen.StatValue(0));
            Assert.Greater(screen.StatBar(0), 0.4f);
            Assert.Less(screen.StatBar(0), 0.6f, "97 of a 200 scale");
            Assert.AreEqual("Arcane Bolt", screen.SkillName(0));
            Assert.AreEqual("Fireball" + System.Environment.NewLine.Substring(System.Environment.NewLine.Length - 1) + "(Frost)", screen.SkillName(1));
            StringAssert.StartsWith("Staff +10% (Sturdy)", screen.WeaponText);
            Assert.AreEqual("Armor: Stone Warden (health +10%)", screen.ArmorText);
        }

        [Test]
        public void ThePictureShowsOnlyWhenTheSkinHasAModel()
        {
            var screen = NewScreen((_, __) => { }, (_, __) => { }, () => { });
            var info = CharacterInfo.Build(progress, classes.Get("staff"), abilities, new int[Entity.AbilitySlots]);

            screen.Show(info, progress);
            Assert.AreEqual(info.Skin != null && info.Skin.prefab != null, screen.PictureShown, "a skin with a model is drawn, one without is left empty");

            info.Skin = null;
            screen.Show(info, progress);
            Assert.IsFalse(screen.PictureShown);
        }

        [Test]
        public void AChosenSkillVariantIsNamedAfterItsSkill()
        {
            var staff = classes.Get("staff");
            var loadout = new int[Entity.AbilitySlots];
            loadout[1] = StableId.From("frost");

            var skills = CharacterInfo.SkillLines(staff, abilities, loadout);

            StringAssert.Contains("Skill: Fireball (Frost)", skills);
        }

        [Test]
        public void TheScreenListsTheBagOfWeaponsAndOfArmorWithWhatTakingApartGives()
        {
            var screen = NewScreen((_, __) => { }, (_, __) => { }, () => { });
            var info = CharacterInfo.Build(progress, classes.Get("staff"), abilities, new int[Entity.AbilitySlots]);

            screen.Show(info, progress);

            Assert.AreEqual(4, screen.VisibleRows, "three starter weapons and one drop");
            var rows = new System.Collections.Generic.List<string>();
            for (var i = 0; i < screen.VisibleRows; i++)
            {
                rows.Add(screen.RowText(i));
            }

            Assert.IsTrue(rows.Exists(r => r.StartsWith("[in hand] Staff +10% (Sturdy)")));
            Assert.AreEqual("in use", screen.DismantleText(3));
            StringAssert.StartsWith("last one", screen.DismantleText(0), "the only sword and shield");
        }

        [Test]
        public void TheArmorTabListsTheSetsAndMarksTheWornOne()
        {
            var info = CharacterInfo.Build(progress, classes.Get("staff"), abilities, new int[Entity.AbilitySlots]);
            CharacterScreen screen = null;
            screen = NewScreen((_, __) => { }, (_, __) => { }, () => screen.Show(info, progress));
            screen.Show(info, progress);

            FindButton("ArmorTab").onClick.Invoke();

            Assert.AreEqual(CharacterScreen.Tab.Armor, screen.CurrentTab);
            Assert.AreEqual(2, screen.VisibleRows, "the starter set and the drop");
            Assert.AreEqual("Wanderer", screen.RowText(0));
            Assert.AreEqual("Take apart +20", screen.DismantleText(0));
            Assert.AreEqual("[worn] Stone Warden (health +10%)", screen.RowText(1));
            Assert.AreEqual("in use", screen.DismantleText(1));
        }

        Button FindButton(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            return null;
        }

        [Test]
        public void TappingARowCarriesItAndTheRightButtonTakesItApartThroughTheCallbacks()
        {
            CharacterScreen.Tab tappedTab = CharacterScreen.Tab.Armor;
            string carried = null;
            string taken = null;
            var screen = NewScreen((t, id) => { tappedTab = t; carried = id; }, (t, id) => taken = id, () => { });
            screen.Show(CharacterInfo.Build(progress, classes.Get("staff"), abilities, new int[Entity.AbilitySlots]), progress);

            FindButton("Item3").onClick.Invoke();
            FindButton("Dismantle1").onClick.Invoke();

            Assert.AreEqual(CharacterScreen.Tab.Weapons, tappedTab);
            Assert.AreEqual("staff-drop1", carried);
            Assert.AreEqual("staff", taken, "the spare staff at row 1");
        }

        [Test]
        public void ADismantleGivesGoldAndShrinksTheBag()
        {
            Assert.IsTrue(progress.DismantleWeapon("staff", out var gold));

            Assert.AreEqual(22, gold, "a starter staff of power 1");
            Assert.AreEqual(140 + gold, progress.Gold);
            Assert.IsFalse(progress.Profile.Has("staff"));
            progress.Flush();
            Assert.AreEqual(140 + gold, new ProfileStore(directory, Starter).Load().Gold, "and it is saved");
        }
    }
}
