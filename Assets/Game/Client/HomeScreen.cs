using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // One way to play. Only the dungeon exists so far; the others are shown as coming soon so the home screen has its final shape.
    public sealed class GameModeEntry
    {
        public GameModeEntry(string id, string name, string description, bool available)
        {
            Id = id;
            Name = name;
            Description = description;
            Available = available;
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public bool Available { get; }
    }

    // The home screen: the character at a glance, a way into the Character screen, and the game modes.
    public sealed class HomeScreen
    {
        public static readonly IReadOnlyList<GameModeEntry> Modes = new[]
        {
            new GameModeEntry("dungeon", "Dungeon", "The slime cave: three bosses, packs, chests and loot", true),
            new GameModeEntry("arena", "Arena", "Coming soon", false),
            new GameModeEntry("idle", "Idle grind", "Coming soon: earn gold and materials while away", false),
            new GameModeEntry("tower", "Tower", "Coming soon", false),
        };

        readonly Text weaponLabel;
        readonly Text armorLabel;
        readonly Text statsLabel;
        readonly Text walletLabel;
        readonly List<UnityEngine.UI.Button> modeButtons = new List<UnityEngine.UI.Button>();

        public HomeScreen(RectTransform parent, Action<string> onPlay, Action onCharacter)
        {
            Label(parent, "Title", "projectBankai", 84, new Vector2(0f, -110f), new Vector2(1000f, 110f));
            Label(parent, "Subtitle", "working title", 30, new Vector2(0f, -215f), new Vector2(1000f, 50f)).color = new Color(1f, 1f, 1f, 0.55f);

            var card = Hud.NewImage("CharacterCard", parent, null, new Color(1f, 1f, 1f, 0.09f), false).rectTransform;
            Place(card, new Vector2(0f, -310f), new Vector2(960f, 470f));
            Label(card, "Heading", "Your character", 40, new Vector2(0f, -22f), new Vector2(900f, 60f)).color = new Color(1f, 0.85f, 0.4f);
            weaponLabel = Label(card, "Weapon", string.Empty, 34, new Vector2(0f, -95f), new Vector2(920f, 56f));
            armorLabel = Label(card, "Armor", string.Empty, 32, new Vector2(0f, -157f), new Vector2(920f, 56f));
            statsLabel = Label(card, "Stats", string.Empty, 34, new Vector2(0f, -219f), new Vector2(920f, 56f));
            walletLabel = Label(card, "Wallet", string.Empty, 32, new Vector2(0f, -281f), new Vector2(920f, 56f));
            Button(card, "OpenCharacter", "Character, weapons and armor", new Vector2(0f, -355f), new Vector2(760f, 90f), onCharacter, true);

            Label(parent, "ModesHeading", "Play", 40, new Vector2(0f, -820f), new Vector2(900f, 60f)).color = new Color(1f, 0.85f, 0.4f);
            for (var i = 0; i < Modes.Count; i++)
            {
                var mode = Modes[i];
                var button = Button(parent, "Mode" + mode.Id, mode.Name + "\n" + mode.Description, new Vector2(0f, -900f - i * 190f), new Vector2(960f, 170f), () => onPlay(mode.Id), mode.Available);
                button.interactable = mode.Available;
                modeButtons.Add(button);
            }

            Label(parent, "Hint", "Co-op: start the dungeon, then Menu, Session to host or join over Wi-Fi", 28, new Vector2(0f, -1700f), new Vector2(980f, 90f)).color = new Color(1f, 1f, 1f, 0.55f);
        }

        public string WeaponText => weaponLabel.text;

        public string ArmorText => armorLabel.text;

        public string StatsText => statsLabel.text;

        public string WalletText => walletLabel.text;

        public bool ModeAvailable(string id)
        {
            for (var i = 0; i < Modes.Count; i++)
            {
                if (Modes[i].Id == id)
                {
                    return modeButtons[i].interactable;
                }
            }

            return false;
        }

        // The card shows what the Character screen shows, in the same words.
        public void Show(CharacterInfo info)
        {
            weaponLabel.text = info.Weapon;
            armorLabel.text = info.Armor;
            statsLabel.text = info.Stats;
            walletLabel.text = info.Wallet;
        }

        static Font ScreenFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Text Label(RectTransform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var rect = Hud.NewRect(name, parent);
            Place(rect, position, dimensions);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = ScreenFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        static UnityEngine.UI.Button Button(RectTransform parent, string name, string text, Vector2 position, Vector2 dimensions, Action onClick, bool enabled)
        {
            var image = Hud.NewImage(name, parent, null, enabled ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.07f), true);
            Place(image.rectTransform, position, dimensions);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var label = Label(image.rectTransform, "Text", text, 34, Vector2.zero, dimensions);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
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
