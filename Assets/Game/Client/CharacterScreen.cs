using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // What the Character screen says about the weapon in hand: the weapon and its mastery, the armor worn, the numbers they add up to,
    // and the skills the weapon gives. Plain text, built from the profile and the definitions so it can be tested.
    public sealed class CharacterInfo
    {
        public string Weapon = string.Empty;
        public string Armor = string.Empty;
        public string Stats = string.Empty;
        public string Wallet = string.Empty;
        public string Skills = string.Empty;

        public static CharacterInfo Build(MasteryProgress progress, ClassDef weaponClass, Registry<AbilityDef> abilities, int[] loadout)
        {
            var profile = progress.Profile;
            var info = new CharacterInfo();
            var weapon = profile.Equipped;
            if (weapon == null || weaponClass == null)
            {
                return info;
            }

            var mastery = profile.MasteryFor(weapon.Type);
            progress.Rules.Progress(mastery.Xp, out var into, out var needed);
            info.Weapon = $"{MasteryProgress.Describe(weapon)}   mastery {mastery.Level(progress.Rules)} ({(needed > 0 ? into + "/" + needed : "max")})";
            info.Armor = profile.EquippedArmor != null ? "Armor: " + progress.DescribeArmor(profile.EquippedArmor) : "Armor: none";

            var gear = progress.Gear;
            info.Stats = $"Health {Math.Round(weaponClass.Stats.MaxHealth * gear.Health)}   Speed {weaponClass.Stats.MoveSpeed * gear.Speed:0.0}   Power {weaponClass.Stats.Power * gear.Power:0.00}";
            info.Wallet = $"Gold {profile.Gold}   Potions {profile.Potions}/{PlayerProfile.MaxPotions}";
            info.Skills = SkillLines(weaponClass, abilities, loadout);
            return info;
        }

        // The four skills the weapon gives, with the variant a skill tree choice swapped in.
        public static string SkillLines(ClassDef weaponClass, Registry<AbilityDef> abilities, int[] loadout)
        {
            var text = new StringBuilder();
            AddSkill(text, "Attack", weaponClass.AutoAttack, 0, abilities, loadout);
            AddSkill(text, "Skill", weaponClass.Skills.Length > 0 ? weaponClass.Skills[0] : null, 1, abilities, loadout);
            AddSkill(text, "Ultimate", weaponClass.Skills.Length > 1 ? weaponClass.Skills[1] : null, Entity.UltimateSlot, abilities, loadout);
            AddSkill(text, "Defence", weaponClass.Defense, Entity.DefenseSlot, abilities, loadout);
            return text.ToString().TrimEnd('\n');
        }

        static void AddSkill(StringBuilder text, string slotName, string abilityId, int slot, Registry<AbilityDef> abilities, int[] loadout)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return;
            }

            text.Append(slotName).Append(": ").Append(QuestLog.Title(abilityId));
            if (loadout != null && slot < loadout.Length && loadout[slot] != 0 && abilities.TryGet(abilityId, out var ability))
            {
                foreach (var variant in ability.Variants)
                {
                    if (StableId.From(variant.Id) == loadout[slot])
                    {
                        text.Append(" (").Append(QuestLog.Title(variant.Id)).Append(')');
                        break;
                    }
                }
            }

            text.Append('\n');
        }
    }

    // The Character screen: what the character carries and wears, its numbers and skills, and the bags (weapons and armor) to
    // change what is in use or take things apart for gold. Carrying something else restarts the session, since the kit and the
    // numbers are sent when it begins.
    public sealed class CharacterScreen
    {
        public const int PerPage = 5;
        const float RowHeight = 110f;

        public enum Tab
        {
            Weapons,
            Armor,
        }

        readonly GameObject root;
        readonly Text weaponLabel;
        readonly Text armorLabel;
        readonly Text statsLabel;
        readonly Text walletLabel;
        readonly Text skillsLabel;
        readonly Text pageLabel;
        readonly Image weaponsTab;
        readonly Image armorTab;
        readonly GameObject[] rows = new GameObject[PerPage];
        readonly Text[] rowLabels = new Text[PerPage];
        readonly Image[] rowImages = new Image[PerPage];
        readonly GameObject[] dismantleButtons = new GameObject[PerPage];
        readonly Text[] dismantleLabels = new Text[PerPage];
        readonly List<string> shownIds = new List<string>();
        readonly Action<string> equip;
        readonly Action<string> dismantle;
        readonly Action refresh;
        int page;

        public CharacterScreen(RectTransform parent, Action<Tab, string> onEquip, Action<Tab, string> onDismantle, Action onChanged, Action onClose)
        {
            refresh = onChanged;
            equip = id => onEquip(CurrentTab, id);
            dismantle = id => onDismantle(CurrentTab, id);

            var panel = Hud.NewImage("CharacterScreen", parent, null, new Color(0.08f, 0.09f, 0.11f, 0.95f), true);
            root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960f, 1700f);
            rect.anchoredPosition = Vector2.zero;

            Label(rect, "Title", "Character", 46, new Vector2(0f, -30f), new Vector2(900f, 70f));
            weaponLabel = Label(rect, "Weapon", string.Empty, 32, new Vector2(0f, -110f), new Vector2(900f, 50f));
            armorLabel = Label(rect, "Armor", string.Empty, 30, new Vector2(0f, -165f), new Vector2(900f, 50f));
            statsLabel = Label(rect, "Stats", string.Empty, 32, new Vector2(0f, -220f), new Vector2(900f, 50f));
            walletLabel = Label(rect, "Wallet", string.Empty, 30, new Vector2(0f, -275f), new Vector2(900f, 50f));
            skillsLabel = Label(rect, "Skills", string.Empty, 30, new Vector2(0f, -335f), new Vector2(860f, 200f));
            skillsLabel.alignment = TextAnchor.UpperLeft;

            var weapons = Button(rect, "WeaponsTab", "Weapons", new Vector2(-220f, -560f), new Vector2(420f, 80f), () => SetTab(Tab.Weapons));
            var armor = Button(rect, "ArmorTab", "Armor", new Vector2(220f, -560f), new Vector2(420f, 80f), () => SetTab(Tab.Armor));
            weaponsTab = weapons.GetComponent<Image>();
            armorTab = armor.GetComponent<Image>();
            pageLabel = Label(rect, "Page", string.Empty, 26, new Vector2(0f, -650f), new Vector2(900f, 40f));

            for (var i = 0; i < PerPage; i++)
            {
                var index = i;
                var top = -700f - i * (RowHeight + 10f);
                var main = Button(rect, "Item" + i, string.Empty, new Vector2(-130f, top), new Vector2(650f, RowHeight), () =>
                {
                    if (index < shownIds.Count)
                    {
                        equip(shownIds[index]);
                    }
                });
                rows[i] = main.gameObject;
                rowImages[i] = main.GetComponent<Image>();
                rowLabels[i] = main.GetComponentInChildren<Text>();
                rowLabels[i].fontSize = 28;

                var take = Button(rect, "Dismantle" + i, string.Empty, new Vector2(335f, top), new Vector2(230f, RowHeight), () =>
                {
                    if (index < shownIds.Count)
                    {
                        dismantle(shownIds[index]);
                    }
                });
                dismantleButtons[i] = take.gameObject;
                dismantleLabels[i] = take.GetComponentInChildren<Text>();
                dismantleLabels[i].fontSize = 24;
                rows[i].SetActive(false);
                dismantleButtons[i].SetActive(false);
            }

            Button(rect, "More", "More", new Vector2(-215f, -1620f), new Vector2(400f, 70f), () =>
            {
                page++;
                refresh?.Invoke();
            });
            Button(rect, "Close", "Close", new Vector2(215f, -1620f), new Vector2(400f, 70f), onClose);
            root.SetActive(false);
        }

        public Tab CurrentTab { get; private set; }

        public bool IsOpen => root.activeSelf;

        public int Page => page;

        public string WeaponText => weaponLabel.text;

        public string ArmorText => armorLabel.text;

        public string StatsText => statsLabel.text;

        public string WalletText => walletLabel.text;

        public string SkillsText => skillsLabel.text;

        public int VisibleRows
        {
            get
            {
                var count = 0;
                foreach (var row in rows)
                {
                    count += row.activeSelf ? 1 : 0;
                }

                return count;
            }
        }

        public string RowText(int index)
        {
            return rowLabels[index].text;
        }

        public string DismantleText(int index)
        {
            return dismantleButtons[index].activeSelf ? dismantleLabels[index].text : string.Empty;
        }

        public void SetOpen(bool open)
        {
            root.SetActive(open);
            if (open)
            {
                page = 0;
            }
        }

        void SetTab(Tab tab)
        {
            CurrentTab = tab;
            page = 0;
            refresh?.Invoke();
        }

        public void Show(CharacterInfo info, MasteryProgress progress)
        {
            weaponLabel.text = info.Weapon;
            armorLabel.text = info.Armor;
            statsLabel.text = info.Stats;
            walletLabel.text = info.Wallet;
            skillsLabel.text = info.Skills;
            weaponsTab.color = CurrentTab == Tab.Weapons ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);
            armorTab.color = CurrentTab == Tab.Armor ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);

            var profile = progress.Profile;
            var count = CurrentTab == Tab.Weapons ? profile.Weapons.Count : profile.Armors.Count;
            var limit = CurrentTab == Tab.Weapons ? PlayerProfile.MaxWeapons : PlayerProfile.MaxArmors;
            var pages = Math.Max(1, (count + PerPage - 1) / PerPage);
            page %= pages;
            pageLabel.text = $"{count} of {limit}   page {page + 1}/{pages}   (tap to carry, right button takes apart for gold)";
            shownIds.Clear();
            for (var i = 0; i < PerPage; i++)
            {
                var index = page * PerPage + i;
                var present = index < count;
                rows[i].SetActive(present);
                dismantleButtons[i].SetActive(present);
                if (!present)
                {
                    continue;
                }

                string id;
                string text;
                bool inUse;
                bool canTakeApart;
                int gold;
                if (CurrentTab == Tab.Weapons)
                {
                    var weapon = profile.Weapons[index];
                    id = weapon.Uid;
                    inUse = id == profile.EquippedUid;
                    text = DescribeWeapon(weapon, profile, progress.Rules, inUse);
                    gold = PlayerProfile.DismantleValue(weapon);
                    canTakeApart = !inUse && profile.Weapons.FindAll(w => w.Type == weapon.Type).Count > 1;
                }
                else
                {
                    var armor = profile.Armors[index];
                    id = armor.Uid;
                    inUse = id == profile.EquippedArmorUid;
                    text = (inUse ? "[worn] " : string.Empty) + progress.DescribeArmor(armor);
                    gold = PlayerProfile.DismantleValue(armor);
                    canTakeApart = !inUse;
                }

                shownIds.Add(id);
                rowLabels[i].text = text;
                rowImages[i].color = inUse ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);
                dismantleLabels[i].text = canTakeApart ? $"Take apart +{gold}" : (inUse ? "in use" : "last one");
                dismantleButtons[i].GetComponent<Image>().color = canTakeApart ? new Color(0.85f, 0.55f, 0.2f, 0.45f) : new Color(1f, 1f, 1f, 0.08f);
            }
        }

        public static string DescribeWeapon(OwnedWeapon weapon, PlayerProfile profile, MasteryRules rules, bool inHand)
        {
            var level = profile.MasteryFor(weapon.Type).Level(rules);
            return $"{(inHand ? "[in hand] " : string.Empty)}{MasteryProgress.Describe(weapon)}   mastery {level}";
        }

        static Font PanelFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Text Label(RectTransform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var rect = Hud.NewRect(name, parent);
            Place(rect, position, dimensions);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = PanelFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        static UnityEngine.UI.Button Button(RectTransform parent, string name, string text, Vector2 position, Vector2 dimensions, Action onClick)
        {
            var image = Hud.NewImage(name, parent, null, new Color(1f, 1f, 1f, 0.16f), true);
            Place(image.rectTransform, position, dimensions);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var label = Label(image.rectTransform, "Text", text, 34, Vector2.zero, dimensions);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            return button;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 dimensions)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
        }
    }
}
