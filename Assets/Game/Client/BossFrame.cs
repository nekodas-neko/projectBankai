using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // The boss's health at the top of the screen and who it is fighting, so everyone can tell when it turns to someone else.
    public sealed class BossFrame
    {
        public static readonly Color Calm = new Color(0.95f, 0.85f, 0.3f, 1f);
        public static readonly Color OnYou = new Color(1f, 0.3f, 0.25f, 1f);

        readonly GameObject root;
        readonly Image fill;
        readonly Text label;

        BossFrame(GameObject root, Image fill, Text label)
        {
            this.root = root;
            this.fill = fill;
            this.label = label;
        }

        public bool IsVisible => root.activeSelf;

        public string Text => label.text;

        public float FillFraction => fill.rectTransform.anchorMax.x;

        public static BossFrame Build(RectTransform parent)
        {
            var background = Hud.NewImage("BossFrame", parent, null, new Color(0f, 0f, 0f, 0.55f), false);
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(560f, 64f);
            rect.anchoredPosition = new Vector2(0f, -145f);

            var fill = Hud.NewImage("Fill", rect, null, new Color(0.8f, 0.2f, 0.2f, 0.95f), false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;

            var label = Hud.NewRect("Label", rect).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Calm;
            label.raycastTarget = false;

            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var frame = new BossFrame(background.gameObject, fill, label);
            frame.Hide();
            return frame;
        }

        public static string TargetText(bool hasTarget, bool isLocal, int playerNumber)
        {
            if (!hasTarget)
            {
                return "Boss - idle";
            }

            return isLocal ? "Boss - targeting YOU" : $"Boss - targeting Player {playerNumber}";
        }

        public void Hide()
        {
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        public void Show(float health, float maxHealth, bool hasTarget, bool targetIsLocal, int playerNumber)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            fill.rectTransform.anchorMax = new Vector2(maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f, 1f);
            var text = TargetText(hasTarget, targetIsLocal, playerNumber);
            if (label.text != text)
            {
                label.text = text;
            }

            label.color = hasTarget && targetIsLocal ? OnYou : Calm;
        }
    }
}
