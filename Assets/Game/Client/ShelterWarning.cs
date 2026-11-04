using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // Shown while a map-wide attack winds up: a red edge tint and a line saying where to stand and how long is left.
    // It turns calm and green once the player is on the shelter, so the answer is clear without reading.
    public sealed class ShelterWarning
    {
        static readonly Color Danger = new Color(0.85f, 0.08f, 0.08f, 0.22f);
        static readonly Color Safe = new Color(0.15f, 0.55f, 0.95f, 0.14f);

        readonly GameObject root;
        readonly Image tint;
        readonly Text text;

        ShelterWarning(GameObject root, Image tint, Text text)
        {
            this.root = root;
            this.tint = tint;
            this.text = text;
        }

        public bool Visible => root.activeSelf;

        public string Text => text.text;

        public bool Sheltered { get; private set; }

        public static ShelterWarning Build(RectTransform parent)
        {
            var tint = Hud.NewImage("ShelterTint", parent, null, Danger, false);
            var label = Hud.NewRect("ShelterText", tint.rectTransform);
            label.anchorMin = new Vector2(0.5f, 0.74f);
            label.anchorMax = new Vector2(0.5f, 0.74f);
            label.pivot = new Vector2(0.5f, 0.5f);
            label.sizeDelta = new Vector2(980f, 140f);
            label.anchoredPosition = Vector2.zero;
            var text = label.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 56;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            var warning = new ShelterWarning(tint.gameObject, tint, text);
            warning.Hide();
            return warning;
        }

        // tag is what shelters (for example "water"); remaining is the seconds until it lands.
        public void Show(string tag, float remaining, bool sheltered)
        {
            Sheltered = sheltered;
            root.SetActive(true);
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
            var colour = sheltered ? Safe : Danger;
            colour.a *= sheltered ? 1f : pulse;
            tint.color = colour;
            var place = string.IsNullOrEmpty(tag) ? "cover" : tag;
            text.text = sheltered ? $"Stay in the {place}  {remaining:0.0}" : $"Get to the {place}!  {remaining:0.0}";
        }

        public void Hide()
        {
            Sheltered = false;
            root.SetActive(false);
        }
    }
}
