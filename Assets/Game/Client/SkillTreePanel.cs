using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // Lists the equipped weapon's skill tree: tap a node to take it (a second node in the same slot swaps it), Reset gives every point back.
    public sealed class SkillTreePanel
    {
        const int MaxRows = 6;
        const float RowHeight = 120f;

        readonly GameObject root;
        readonly Text title;
        readonly Text status;
        readonly GameObject[] rows = new GameObject[MaxRows];
        readonly Text[] rowLabels = new Text[MaxRows];
        readonly Image[] rowImages = new Image[MaxRows];

        public SkillTreePanel(RectTransform parent, Action<int> onNode, Action onReset, Action onClose)
        {
            var panel = Hud.NewImage("SkillTreePanel", parent, null, new Color(0.08f, 0.09f, 0.11f, 0.94f), true);
            root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 1300f);
            rect.anchoredPosition = Vector2.zero;

            title = Label(rect, "Title", string.Empty, 44, new Vector2(0f, -50f), new Vector2(840f, 70f));
            status = Label(rect, "Status", string.Empty, 28, new Vector2(0f, -130f), new Vector2(840f, 110f));

            for (var i = 0; i < MaxRows; i++)
            {
                var index = i;
                var button = Button(rect, "Node" + i, string.Empty, new Vector2(0f, -260f - i * (RowHeight + 10f)), new Vector2(820f, RowHeight), () => onNode(index));
                rows[i] = button.gameObject;
                rowImages[i] = button.GetComponent<Image>();
                rowLabels[i] = button.GetComponentInChildren<Text>();
                rowLabels[i].fontSize = 30;
                rows[i].SetActive(false);
            }

            Button(rect, "Reset", "Reset points", new Vector2(-215f, -1210f), new Vector2(400f, 80f), onReset);
            Button(rect, "Close", "Close", new Vector2(215f, -1210f), new Vector2(400f, 80f), onClose);
            root.SetActive(false);
        }

        public bool IsOpen => root.activeSelf;

        public void SetOpen(bool open)
        {
            root.SetActive(open);
        }

        public void Show(string weapon, SkillTreeDef tree, WeaponMastery mastery, MasteryRules rules, string message)
        {
            title.text = MasteryProgress.Name(weapon) + " skills";
            var level = mastery.Level(rules);
            status.text = $"Mastery {level}   Points left: {mastery.PointsAvailable(rules)}" + (string.IsNullOrEmpty(message) ? string.Empty : "\n" + message);
            for (var i = 0; i < MaxRows; i++)
            {
                var node = tree != null && i < tree.Nodes.Length ? tree.Nodes[i] : null;
                rows[i].SetActive(node != null);
                if (node == null)
                {
                    continue;
                }

                var taken = mastery.Choices.Contains(node.Id);
                rowLabels[i].text = Describe(tree, node, taken);
                rowImages[i].color = taken ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);
            }
        }

        public static string Describe(SkillTreeDef tree, SkillNodeDef node, bool taken)
        {
            var needs = node.Requires.Length > 0 ? "  (needs " + (tree.Find(node.Requires)?.Name ?? node.Requires) + ")" : string.Empty;
            return $"{(taken ? "[x] " : string.Empty)}{node.Name}   {SlotName(node.Slot)}   {node.Cost} pt{needs}";
        }

        public static string SlotName(int slot)
        {
            switch (slot)
            {
                case 0: return "Attack";
                case 1: return "Skill";
                case 2: return "Ultimate";
                default: return slot == Entity.DefenseSlot ? "Defence" : "Slot " + slot;
            }
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
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
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
