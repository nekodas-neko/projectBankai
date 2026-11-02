using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public sealed class ResourceBar
    {
        readonly GameObject root;
        readonly Image fill;
        readonly Text label;

        ResourceBar(GameObject root, Image fill, Text label)
        {
            this.root = root;
            this.fill = fill;
            this.label = label;
        }

        public static Color ColorFor(string resourceId)
        {
            switch (resourceId)
            {
                case "mana": return new Color(0.25f, 0.55f, 0.95f, 0.95f);
                case "rage": return new Color(0.9f, 0.3f, 0.25f, 0.95f);
                case "focus": return new Color(0.7f, 0.4f, 0.95f, 0.95f);
                default: return new Color(0.8f, 0.8f, 0.8f, 0.95f);
            }
        }

        public static ResourceBar Build(RectTransform parent)
        {
            var background = Hud.NewImage("ResourceBar", parent, null, new Color(0f, 0f, 0f, 0.5f), false);
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(420f, 44f);
            rect.anchoredPosition = new Vector2(40f, -60f);

            var fill = Hud.NewImage("Fill", rect, null, Color.white, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;

            var label = Hud.NewRect("Label", rect).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var bar = new ResourceBar(background.gameObject, fill, label);
            bar.Hide();
            return bar;
        }

        public void Hide()
        {
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        public void Show(string name, float value, float max)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            fill.color = ColorFor(name);
            fill.rectTransform.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(value / max) : 0f, 1f);
            var text = name + " " + Mathf.FloorToInt(value);
            if (label.text != text)
            {
                label.text = text;
            }
        }
    }
}
