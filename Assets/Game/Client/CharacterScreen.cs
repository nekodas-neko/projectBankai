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

        // The same numbers as separate values (for the bars), the skill slots (for the icons) and the skin (for the picture).
        public float Health;
        public float Speed;
        public float Power;
        public readonly List<SkillSlot> SkillSlots = new List<SkillSlot>();
        public SkinAsset Skin;

        public sealed class SkillSlot
        {
            public string Label = string.Empty;
            public string Ability = string.Empty;
            public string Variant = string.Empty;
        }

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
            info.Health = (float)Math.Round(weaponClass.Stats.MaxHealth * gear.Health);
            info.Speed = weaponClass.Stats.MoveSpeed * gear.Speed;
            info.Power = weaponClass.Stats.Power * gear.Power;
            info.Stats = $"Health {info.Health}   Speed {info.Speed:0.0}   Power {info.Power:0.00}";
            info.Skin = SkinCatalog.Get(StableId.From(HudSettings.SkinFor(weaponClass.Id)));
            info.Wallet = $"Gold {profile.Gold}   Potions {profile.Potions}/{PlayerProfile.MaxPotions}";
            info.Skills = SkillLines(weaponClass, abilities, loadout);
            AddSlot(info, "Attack", weaponClass.AutoAttack, 0, abilities, loadout);
            AddSlot(info, "Skill", weaponClass.Skills.Length > 0 ? weaponClass.Skills[0] : null, 1, abilities, loadout);
            AddSlot(info, "Ultimate", weaponClass.Skills.Length > 1 ? weaponClass.Skills[1] : null, Entity.UltimateSlot, abilities, loadout);
            AddSlot(info, "Defence", weaponClass.Defense, Entity.DefenseSlot, abilities, loadout);
            return info;
        }

        static void AddSlot(CharacterInfo info, string label, string abilityId, int slot, Registry<AbilityDef> abilities, int[] loadout)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return;
            }

            var entry = new SkillSlot { Label = label, Ability = QuestLog.Title(abilityId) };
            if (loadout != null && slot < loadout.Length && loadout[slot] != 0 && abilities.TryGet(abilityId, out var ability))
            {
                foreach (var variant in ability.Variants)
                {
                    if (StableId.From(variant.Id) == loadout[slot])
                    {
                        entry.Variant = QuestLog.Title(variant.Id);
                        break;
                    }
                }
            }

            info.SkillSlots.Add(entry);
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


    // The Character screen, laid out like a classic character sheet: a picture of the character on the right, the numbers as bars and
    // the skills as round icons on the left, and below them the bags (weapons and armor) to change what is in use or take things
    // apart for gold. Carrying something else restarts the session, since the kit and the numbers are sent when it begins.
    public sealed class CharacterScreen
    {
        public const int PerPage = 5;
        const float RowHeight = 104f;

        static readonly Color Gold = new Color(0.95f, 0.82f, 0.5f);
        static readonly Color Frame = new Color(0.62f, 0.48f, 0.22f);
        static readonly Color Ribbon = new Color(0.50f, 0.10f, 0.10f, 0.96f);
        static readonly Color Dark = new Color(0.07f, 0.05f, 0.05f, 0.97f);
        static readonly Color Chosen = new Color(0.50f, 0.10f, 0.10f, 0.9f);
        static readonly Color Plain = new Color(1f, 1f, 1f, 0.12f);

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
        readonly Text[] statValues = new Text[3];
        readonly Image[] statBars = new Image[3];
        readonly Image[] skillIcons = new Image[4];
        readonly Text[] skillNames = new Text[4];
        readonly RawImage pictureImage;
        readonly Image weaponsTab;
        readonly Image armorTab;
        readonly GameObject[] rows = new GameObject[PerPage];
        readonly Text[] rowLabels = new Text[PerPage];
        readonly Image[] rowImages = new Image[PerPage];
        readonly GameObject[] dismantleButtons = new GameObject[PerPage];
        readonly Text[] dismantleLabels = new Text[PerPage];
        readonly List<string> shownIds = new List<string>();
        readonly Action<Tab, string> onEquip;
        readonly Action<Tab, string> onDismantle;
        readonly Action refresh;
        CharacterPreview preview;
        SkinAsset previewSkin;
        int page;

        public CharacterScreen(RectTransform parent, Action<Tab, string> onEquip, Action<Tab, string> onDismantle, Action onChanged, Action onClose)
        {
            this.onEquip = onEquip;
            this.onDismantle = onDismantle;
            refresh = onChanged;

            var outer = Hud.NewImage("CharacterScreen", parent, null, Frame, true);
            root = outer.gameObject;
            var rect = outer.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(980f, 1720f);
            rect.anchoredPosition = Vector2.zero;
            var inner = Hud.NewImage("Inner", rect, null, Dark, false).rectTransform;
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            var body = Hud.NewRect("Body", rect);
            body.anchorMin = new Vector2(0.5f, 0.5f);
            body.anchorMax = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(960f, 1700f);
            body.anchoredPosition = Vector2.zero;

            Banner(body, "Title", "CHARACTER", 44, new Vector2(0f, -22f), new Vector2(560f, 76f));

            // Left: the numbers as bars, then the skills as round icons.
            var left = Panel(body, "Characteristics", new Vector2(-245f, -120f), new Vector2(450f, 730f));
            Banner(left, "CharacteristicsBanner", "CHARACTERISTICS", 26, new Vector2(0f, -14f), new Vector2(410f, 50f));
            var names = new[] { "HEALTH", "SPEED", "POWER" };
            for (var i = 0; i < 3; i++)
            {
                var y = -92f - i * 66f;
                Caption(left, "StatName" + i, names[i], new Vector2(-95f, y), new Vector2(140f, 40f), TextAnchor.MiddleLeft);
                var back = Hud.NewImage("StatBack" + i, left, null, new Color(0f, 0f, 0f, 0.6f), false).rectTransform;
                Place(back, new Vector2(35f, y - 8f), new Vector2(190f, 22f));
                statBars[i] = Hud.NewImage("StatFill" + i, back, null, new Color(0.78f, 0.62f, 0.22f), false);
                statBars[i].rectTransform.anchorMin = Vector2.zero;
                statBars[i].rectTransform.anchorMax = new Vector2(0f, 1f);
                statBars[i].rectTransform.offsetMin = Vector2.zero;
                statBars[i].rectTransform.offsetMax = Vector2.zero;
                statValues[i] = Caption(left, "StatValue" + i, string.Empty, new Vector2(165f, y), new Vector2(110f, 40f), TextAnchor.MiddleRight);
            }

            walletLabel = Caption(left, "Wallet", string.Empty, new Vector2(0f, -300f), new Vector2(420f, 44f), TextAnchor.MiddleCenter);
            Banner(left, "SkillsBanner", "SKILLS", 26, new Vector2(0f, -368f), new Vector2(410f, 50f));
            var colours = new[] { new Color(0.22f, 0.54f, 0.87f), new Color(0.50f, 0.47f, 0.87f), new Color(0.93f, 0.62f, 0.18f), new Color(0.30f, 0.62f, 0.45f) };
            var letters = new[] { "A", "S", "U", "D" };
            for (var i = 0; i < 4; i++)
            {
                var x = -156f + i * 104f;
                var icon = Hud.NewImage("SkillIcon" + i, left, ProceduralSprites.Disc, colours[i], false);
                Place(icon.rectTransform, new Vector2(x, -440f), new Vector2(84f, 84f));
                var letter = Caption(icon.rectTransform, "Letter", letters[i], Vector2.zero, new Vector2(84f, 84f), TextAnchor.MiddleCenter);
                letter.rectTransform.anchorMin = Vector2.zero;
                letter.rectTransform.anchorMax = Vector2.one;
                letter.rectTransform.offsetMin = Vector2.zero;
                letter.rectTransform.offsetMax = Vector2.zero;
                letter.fontSize = 38;
                skillIcons[i] = icon;
                skillNames[i] = Caption(left, "SkillName" + i, string.Empty, new Vector2(x, -532f), new Vector2(104f, 130f), TextAnchor.UpperCenter);
                skillNames[i].fontSize = 19;
            }

            skillsLabel = Caption(left, "SkillsText", string.Empty, Vector2.zero, new Vector2(1f, 1f), TextAnchor.MiddleCenter);
            skillsLabel.gameObject.SetActive(false);

            // Right: the picture of the character with the weapon and armor named under it.
            var right = Panel(body, "Picture", new Vector2(245f, -120f), new Vector2(450f, 730f));
            var picture = Hud.NewRect("Picture", right);
            picture.anchorMin = new Vector2(0f, 0f);
            picture.anchorMax = new Vector2(1f, 1f);
            picture.offsetMin = new Vector2(10f, 126f);
            picture.offsetMax = new Vector2(-10f, -10f);
            pictureImage = picture.gameObject.AddComponent<RawImage>();
            pictureImage.color = new Color(1f, 1f, 1f, 0f);
            pictureImage.raycastTarget = false;
            var ribbon = Hud.NewImage("NameRibbon", right, null, Ribbon, false).rectTransform;
            ribbon.anchorMin = new Vector2(0f, 0f);
            ribbon.anchorMax = new Vector2(1f, 0f);
            ribbon.pivot = new Vector2(0.5f, 0f);
            ribbon.sizeDelta = new Vector2(-20f, 112f);
            ribbon.anchoredPosition = new Vector2(0f, 10f);
            weaponLabel = Caption(ribbon, "WeaponName", string.Empty, new Vector2(0f, -4f), new Vector2(420f, 52f), TextAnchor.MiddleCenter);
            weaponLabel.fontSize = 26;
            weaponLabel.color = Gold;
            armorLabel = Caption(ribbon, "ArmorName", string.Empty, new Vector2(0f, -56f), new Vector2(420f, 48f), TextAnchor.MiddleCenter);
            armorLabel.fontSize = 22;
            statsLabel = Caption(body, "StatsText", string.Empty, Vector2.zero, new Vector2(1f, 1f), TextAnchor.MiddleCenter);
            statsLabel.gameObject.SetActive(false);

            // Below: the bags.
            weaponsTab = TabButton(body, "WeaponsTab", "WEAPONS", new Vector2(-235f, -640f), () => SetTab(Tab.Weapons));
            armorTab = TabButton(body, "ArmorTab", "ARMOR", new Vector2(235f, -640f), () => SetTab(Tab.Armor));
            pageLabel = Caption(body, "Page", string.Empty, new Vector2(0f, -745f), new Vector2(920f, 40f), TextAnchor.MiddleCenter);
            pageLabel.fontSize = 22;

            for (var i = 0; i < PerPage; i++)
            {
                var index = i;
                var top = -800f - i * (RowHeight + 8f);
                var main = Button(body, "Item" + i, string.Empty, new Vector2(-130f, top), new Vector2(660f, RowHeight), () =>
                {
                    if (index < shownIds.Count)
                    {
                        this.onEquip(CurrentTab, shownIds[index]);
                    }
                });
                rows[i] = main.gameObject;
                rowImages[i] = main.GetComponent<Image>();
                rowLabels[i] = main.GetComponentInChildren<Text>();
                rowLabels[i].fontSize = 27;

                var take = Button(body, "Dismantle" + i, string.Empty, new Vector2(345f, top), new Vector2(230f, RowHeight), () =>
                {
                    if (index < shownIds.Count)
                    {
                        this.onDismantle(CurrentTab, shownIds[index]);
                    }
                });
                dismantleButtons[i] = take.gameObject;
                dismantleLabels[i] = take.GetComponentInChildren<Text>();
                dismantleLabels[i].fontSize = 23;
                rows[i].SetActive(false);
                dismantleButtons[i].SetActive(false);
            }

            Button(body, "More", "MORE", new Vector2(-215f, -1640f), new Vector2(400f, 66f), () =>
            {
                page++;
                refresh?.Invoke();
            }).GetComponent<Image>().color = Chosen;
            Button(body, "Close", "CLOSE", new Vector2(215f, -1640f), new Vector2(400f, 66f), onClose).GetComponent<Image>().color = Chosen;
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

        public bool PictureShown => pictureImage.color.a > 0.5f;

        public float StatBar(int index)
        {
            return statBars[index].rectTransform.anchorMax.x;
        }

        public string StatValue(int index)
        {
            return statValues[index].text;
        }

        public string SkillName(int index)
        {
            return skillNames[index].text;
        }

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

            preview?.SetRunning(open);
        }

        // Frees the picture's camera and texture (the screen is going away with its scene).
        public void Dispose()
        {
            preview?.Dispose();
            preview = null;
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
            SetBar(0, info.Health, 200f, info.Health.ToString("0"));
            SetBar(1, info.Speed, 9f, info.Speed.ToString("0.0"));
            SetBar(2, info.Power, 1.5f, info.Power.ToString("0.00"));
            for (var i = 0; i < 4; i++)
            {
                var slot = i < info.SkillSlots.Count ? info.SkillSlots[i] : null;
                skillIcons[i].gameObject.SetActive(slot != null);
                skillNames[i].text = slot == null ? string.Empty : slot.Ability + (slot.Variant.Length > 0 ? "\n(" + slot.Variant + ")" : string.Empty);
            }

            ShowPicture(info.Skin);
            weaponsTab.color = CurrentTab == Tab.Weapons ? Chosen : Plain;
            armorTab.color = CurrentTab == Tab.Armor ? Chosen : Plain;

            var profile = progress.Profile;
            var count = CurrentTab == Tab.Weapons ? profile.Weapons.Count : profile.Armors.Count;
            var limit = CurrentTab == Tab.Weapons ? PlayerProfile.MaxWeapons : PlayerProfile.MaxArmors;
            var pages = Math.Max(1, (count + PerPage - 1) / PerPage);
            page %= pages;
            pageLabel.text = $"{count} of {limit}   page {page + 1}/{pages}   tap to carry, right button takes apart for gold";
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
                rowImages[i].color = inUse ? Chosen : Plain;
                dismantleLabels[i].text = canTakeApart ? $"Take apart +{gold}" : (inUse ? "in use" : "last one");
                dismantleButtons[i].GetComponent<Image>().color = canTakeApart ? new Color(0.78f, 0.55f, 0.15f, 0.55f) : new Color(1f, 1f, 1f, 0.07f);
            }
        }

        void SetBar(int index, float value, float full, string text)
        {
            statBars[index].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / full), 1f);
            statValues[index].text = text;
        }

        // Puts the skin's model in the picture. A skin with no model leaves the frame empty.
        void ShowPicture(SkinAsset skin)
        {
            if (skin != previewSkin || preview == null)
            {
                previewSkin = skin;
                if (preview == null)
                {
                    preview = new CharacterPreview();
                    pictureImage.texture = preview.Texture;
                }

                preview.Show(skin);
            }

            pictureImage.color = new Color(1f, 1f, 1f, preview.HasModel ? 1f : 0f);
            preview.SetRunning(IsOpen);
        }

        public static string DescribeWeapon(OwnedWeapon weapon, PlayerProfile profile, MasteryRules rules, bool inHand)
        {
            var level = profile.MasteryFor(weapon.Type).Level(rules);
            return $"{(inHand ? "[in hand] " : string.Empty)}{MasteryProgress.Describe(weapon)}   mastery {level}";
        }

        static Font PanelFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // A framed dark panel: a gold border around a dark middle.
        static RectTransform Panel(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var border = Hud.NewImage(name, parent, null, Frame, false).rectTransform;
            Place(border, position, size);
            var inner = Hud.NewImage("Inner", border, null, new Color(0.09f, 0.06f, 0.06f, 1f), false).rectTransform;
            inner.offsetMin = new Vector2(4f, 4f);
            inner.offsetMax = new Vector2(-4f, -4f);
            var content = Hud.NewRect("Content", border);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = size;
            content.anchoredPosition = Vector2.zero;
            return content;
        }

        static void Banner(RectTransform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var ribbon = Hud.NewImage(name, parent, null, Ribbon, false).rectTransform;
            Place(ribbon, position, dimensions);
            var label = Caption(ribbon, "Text", text, Vector2.zero, dimensions, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = size;
            label.color = Gold;
        }

        static Text Caption(RectTransform parent, string name, string text, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            var rect = Hud.NewRect(name, parent);
            Place(rect, position, dimensions);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = PanelFont;
            label.fontSize = 26;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = text;
            return label;
        }

        static Image TabButton(RectTransform parent, string name, string text, Vector2 position, Action onClick)
        {
            var button = Button(parent, name, text, position, new Vector2(440f, 76f), onClick);
            return button.GetComponent<Image>();
        }

        static UnityEngine.UI.Button Button(RectTransform parent, string name, string text, Vector2 position, Vector2 dimensions, Action onClick)
        {
            var image = Hud.NewImage(name, parent, null, Plain, true);
            Place(image.rectTransform, position, dimensions);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var label = Caption(image.rectTransform, "Text", text, Vector2.zero, dimensions, TextAnchor.MiddleCenter);
            label.fontSize = 32;
            label.color = Gold;
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
