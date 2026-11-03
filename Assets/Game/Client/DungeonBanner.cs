using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public sealed class DungeonBanner
    {
        readonly Text roomLabel;
        readonly Text message;
        readonly GameObject restartButton;
        readonly Text restartLabel;

        DungeonBanner(Text roomLabel, Text message, GameObject restartButton, Text restartLabel)
        {
            this.roomLabel = roomLabel;
            this.message = message;
            this.restartButton = restartButton;
            this.restartLabel = restartLabel;
        }

        public string RoomText => roomLabel.text;

        public string MessageText => message.text;

        public bool RestartVisible => restartButton.activeSelf;

        public static DungeonBanner Build(RectTransform parent, Action onRestart)
        {
            var room = NewText("RoomLabel", parent, 40, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(300f, 70f));
            var message = NewText("DungeonMessage", parent, 64, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(900f, 260f));

            var buttonImage = Hud.NewImage("RestartButton", parent, null, new Color(0.15f, 0.55f, 0.35f, 0.95f), true);
            var rect = buttonImage.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(460f, 130f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => onRestart?.Invoke());
            var label = NewText("Label", rect, 48, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 110f));
            buttonImage.gameObject.SetActive(false);

            var banner = new DungeonBanner(room, message, buttonImage.gameObject, label);
            banner.Hide();
            return banner;
        }

        public void Hide()
        {
            roomLabel.text = string.Empty;
            message.text = string.Empty;
            restartButton.SetActive(false);
        }

        // In a continuous dungeon nobody is moved when a boss falls: a gate opens, and the fallen get back up.
        public void Update(Snapshot snapshot, bool localDown, bool continuous = false)
        {
            if (snapshot == null || snapshot.Dungeon == DungeonPhase.None || snapshot.RoomCount == 0)
            {
                Hide();
                return;
            }

            SetText(roomLabel, $"Room {snapshot.Room} / {snapshot.RoomCount}");
            string text;
            var showRestart = false;
            switch (snapshot.Dungeon)
            {
                case DungeonPhase.Cleared:
                    text = continuous ? "Boss down!\nThe way ahead is open" : $"Room cleared!\nNext room in {Mathf.CeilToInt(snapshot.DungeonTimer)}";
                    break;
                case DungeonPhase.Victory:
                    text = "Victory!";
                    showRestart = true;
                    restartLabel.text = "Play again";
                    break;
                case DungeonPhase.Defeat:
                    text = "Defeat";
                    showRestart = true;
                    restartLabel.text = "Try again";
                    break;
                default:
                    text = localDown ? (continuous ? "You are down\nHold on until the boss falls" : "You are down\nHold on until the room is cleared") : string.Empty;
                    break;
            }

            SetText(message, text);
            if (restartButton.activeSelf != showRestart)
            {
                restartButton.SetActive(showRestart);
            }
        }

        static void SetText(Text target, string value)
        {
            if (target.text != value)
            {
                target.text = value;
            }
        }

        static Text NewText(string name, Transform parent, int size, Vector2 anchor, Vector2 position, Vector2 dimensions)
        {
            var rect = Hud.NewRect(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}
