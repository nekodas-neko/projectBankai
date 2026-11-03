using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // The boss's health at the top of the screen and who it is fighting, so everyone can tell when it turns to someone else.
    public sealed class BossFrame
    {
        public static readonly Color Calm = new Color(0.95f, 0.85f, 0.3f, 1f);
        public static readonly Color OnYou = new Color(1f, 0.3f, 0.25f, 1f);
        public static readonly Color Casting = new Color(0.45f, 0.75f, 1f, 1f);

        readonly GameObject root;
        readonly Image fill;
        readonly Text label;
        readonly GameObject castRoot;
        readonly Image castFill;
        readonly Text castLabel;

        BossFrame(GameObject root, Image fill, Text label, GameObject castRoot, Image castFill, Text castLabel)
        {
            this.root = root;
            this.fill = fill;
            this.label = label;
            this.castRoot = castRoot;
            this.castFill = castFill;
            this.castLabel = castLabel;
        }

        public bool IsVisible => root.activeSelf;

        public string Text => label.text;

        public float FillFraction => fill.rectTransform.anchorMax.x;

        public bool CastVisible => castRoot.activeSelf;

        public string CastText => castLabel.text;

        public float CastFraction => castFill.rectTransform.anchorMax.x;

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

            // The cast bar hangs under the boss frame while the boss winds up something that can be interrupted.
            var castBack = Hud.NewImage("CastBar", rect, null, new Color(0f, 0f, 0f, 0.55f), false);
            var castRect = castBack.rectTransform;
            castRect.anchorMin = new Vector2(0f, 0f);
            castRect.anchorMax = new Vector2(1f, 0f);
            castRect.pivot = new Vector2(0.5f, 1f);
            castRect.sizeDelta = new Vector2(0f, 40f);
            castRect.anchoredPosition = new Vector2(0f, -4f);
            var castFill = Hud.NewImage("Fill", castRect, null, new Color(0.35f, 0.65f, 1f, 0.95f), false);
            castFill.rectTransform.anchorMin = Vector2.zero;
            castFill.rectTransform.anchorMax = Vector2.one;
            var castLabel = Hud.NewRect("Label", castRect).gameObject.AddComponent<Text>();
            castLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            castLabel.fontSize = 26;
            castLabel.alignment = TextAnchor.MiddleCenter;
            castLabel.color = Color.white;
            castLabel.raycastTarget = false;
            var castShadow = castLabel.gameObject.AddComponent<Shadow>();
            castShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            castShadow.effectDistance = new Vector2(2f, -2f);

            var frame = new BossFrame(background.gameObject, fill, label, castBack.gameObject, castFill, castLabel);
            frame.Hide();
            return frame;
        }

        // "brood_call" becomes "Interrupt: Brood call!".
        public static string CastLabel(string abilityId)
        {
            var name = string.IsNullOrEmpty(abilityId) ? "cast" : abilityId.Replace('_', ' ');
            return "Interrupt: " + char.ToUpperInvariant(name[0]) + name.Substring(1) + "!";
        }

        public static string TargetText(bool hasTarget, bool isLocal, int playerNumber, bool staggered = false)
        {
            if (staggered)
            {
                return "Boss - staggered!";
            }

            if (!hasTarget)
            {
                return "Boss - idle";
            }

            return isLocal ? "Boss - targeting YOU" : $"Boss - targeting Player {playerNumber}";
        }

        public void Hide()
        {
            HideCast();
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        public void ShowCast(string abilityId, float fraction)
        {
            if (!castRoot.activeSelf)
            {
                castRoot.SetActive(true);
            }

            castFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            var text = CastLabel(abilityId);
            if (castLabel.text != text)
            {
                castLabel.text = text;
            }
        }

        public void HideCast()
        {
            if (castRoot.activeSelf)
            {
                castRoot.SetActive(false);
            }
        }

        public void Show(float health, float maxHealth, bool hasTarget, bool targetIsLocal, int playerNumber, bool staggered = false)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            fill.rectTransform.anchorMax = new Vector2(maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f, 1f);
            var text = TargetText(hasTarget, targetIsLocal, playerNumber, staggered);
            if (label.text != text)
            {
                label.text = text;
            }

            label.color = staggered ? Casting : hasTarget && targetIsLocal ? OnYou : Calm;
        }
    }
}
