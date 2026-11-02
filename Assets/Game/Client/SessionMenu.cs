using System;
using System.Collections.Generic;
using System.Net;
using Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public sealed class SessionMenu
    {
        const int FoundSlots = 3;

        readonly GameObject root;
        readonly Text status;
        readonly Text[] foundLabels = new Text[FoundSlots];
        readonly GameObject[] foundButtons = new GameObject[FoundSlots];
        readonly IPEndPoint[] foundEndpoints = new IPEndPoint[FoundSlots];
        readonly InputField address;

        public SessionMenu(RectTransform parent, Action onSolo, Action onHost, Action<string> onJoin, Action onClose)
        {
            var panel = Hud.NewImage("SessionMenu", parent, null, new Color(0.08f, 0.09f, 0.11f, 0.94f), true);
            root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 1300f);
            rect.anchoredPosition = Vector2.zero;

            Label(rect, "Title", "Session", 44, new Vector2(0f, -60f), new Vector2(800f, 70f));
            status = Label(rect, "Status", string.Empty, 32, new Vector2(0f, -170f), new Vector2(820f, 150f));

            Button(rect, "Solo", "Solo (offline)", new Vector2(0f, -330f), new Vector2(800f, 100f), onSolo);
            Button(rect, "Host", "Host on Wi-Fi", new Vector2(0f, -450f), new Vector2(800f, 100f), onHost);
            address = Input(rect, new Vector2(-130f, -570f), new Vector2(520f, 100f));
            address.text = HudSettings.JoinAddress;
            Button(rect, "JoinAddress", "Join", new Vector2(280f, -570f), new Vector2(240f, 100f), () => onJoin(address.text));
            Label(rect, "AddressHint", "Type the host's address, for example 192.168.1.20", 26, new Vector2(0f, -680f), new Vector2(820f, 44f));
            Label(rect, "FoundTitle", "Sessions found on this Wi-Fi", 30, new Vector2(0f, -760f), new Vector2(800f, 50f));

            for (var i = 0; i < FoundSlots; i++)
            {
                var slot = i;
                var button = Button(rect, "Found" + i, string.Empty, new Vector2(0f, -830f - i * 115f), new Vector2(800f, 100f), () => JoinFound(slot, onJoin));
                foundButtons[i] = button.gameObject;
                foundLabels[i] = button.GetComponentInChildren<Text>();
                foundButtons[i].SetActive(false);
            }

            Button(rect, "Close", "Close", new Vector2(0f, -1200f), new Vector2(400f, 80f), onClose);
            root.SetActive(false);
        }

        public bool IsOpen => root.activeSelf;

        public void SetOpen(bool open)
        {
            root.SetActive(open);
        }

        public void SetStatus(string text)
        {
            status.text = text;
        }

        public void SetFound(IReadOnlyList<LanScanner.Session> sessions)
        {
            for (var i = 0; i < FoundSlots; i++)
            {
                var present = sessions != null && i < sessions.Count;
                foundButtons[i].SetActive(present);
                if (present)
                {
                    foundEndpoints[i] = sessions[i].EndPoint;
                    foundLabels[i].text = $"{sessions[i].Name}  {sessions[i].Players}/{sessions[i].MaxPlayers}  {sessions[i].EndPoint.Address}";
                }
            }
        }

        void JoinFound(int slot, Action<string> onJoin)
        {
            if (foundEndpoints[slot] != null)
            {
                onJoin(foundEndpoints[slot].ToString());
            }
        }

        static Font MenuFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Text Label(RectTransform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var rect = Hud.NewRect(name, parent);
            Place(rect, position, dimensions);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = MenuFont;
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
            Label(image.rectTransform, "Text", text, 34, Vector2.zero, dimensions);
            var label = image.rectTransform.Find("Text");
            var labelRect = (RectTransform)label;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        static InputField Input(RectTransform parent, Vector2 position, Vector2 dimensions)
        {
            var image = Hud.NewImage("Address", parent, null, new Color(1f, 1f, 1f, 0.9f), true);
            Place(image.rectTransform, position, dimensions);

            var text = Label(image.rectTransform, "Text", string.Empty, 34, Vector2.zero, dimensions);
            text.color = Color.black;
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);

            var field = image.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.targetGraphic = image;
            field.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            field.characterLimit = 24;
            return field;
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
