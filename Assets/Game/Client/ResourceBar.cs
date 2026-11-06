using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public sealed class ResourceBar
    {
        readonly GameObject root;
        readonly Image fill;
        readonly Text label;

        public float FillFraction => fill.fillAmount;

        public string Text => label.text;

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

        // A ring around the skill button that fills with the weapon's resource, with its name and number on the lower half of the button.
        // It is made as a sibling drawn behind the button, in the same place and a little bigger.
        public static ResourceBar Build(RectTransform parent, RectTransform around)
        {
            var size = around.sizeDelta + new Vector2(44f, 44f);
            var track = Hud.NewImage("ResourceTrack", parent, ProceduralSprites.Disc, new Color(0f, 0f, 0f, 0.5f), false);
            Place(track.rectTransform, around, size);
            var fill = Hud.NewImage("ResourceRing", parent, ProceduralSprites.Disc, Color.white, false);
            Place(fill.rectTransform, around, size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            var index = around.GetSiblingIndex();
            track.rectTransform.SetSiblingIndex(index);
            fill.rectTransform.SetSiblingIndex(index + 1);

            var label = Hud.NewRect("ResourceLabel", parent).gameObject.AddComponent<Text>();
            Place(label.rectTransform, around, new Vector2(around.sizeDelta.x, 30f));
            label.rectTransform.anchoredPosition += new Vector2(0f, -around.sizeDelta.y * 0.28f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var bar = new ResourceBar(track.gameObject, fill, label);
            bar.Hide();
            return bar;
        }

        static void Place(RectTransform rect, RectTransform around, Vector2 size)
        {
            rect.anchorMin = around.anchorMin;
            rect.anchorMax = around.anchorMax;
            rect.pivot = around.pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = around.anchoredPosition;
        }

        public void Hide()
        {
            if (root.activeSelf)
            {
                root.SetActive(false);
                fill.gameObject.SetActive(false);
                label.gameObject.SetActive(false);
            }
        }

        public void Show(string name, float value, float max)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
                fill.gameObject.SetActive(true);
                label.gameObject.SetActive(true);
            }

            fill.color = ColorFor(name);
            fill.fillAmount = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            var text = name + " " + Mathf.FloorToInt(value);
            if (label.text != text)
            {
                label.text = text;
            }
        }
    }
}
