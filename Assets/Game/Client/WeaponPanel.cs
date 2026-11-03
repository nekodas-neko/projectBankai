using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    // The weapon bag: every weapon the character owns, six to a page. Tap one to carry it (the session restarts with it).
    public sealed class WeaponPanel
    {
        public const int PerPage = 6;
        const float RowHeight = 120f;

        readonly GameObject root;
        readonly Text pageLabel;
        readonly GameObject[] rows = new GameObject[PerPage];
        readonly Text[] rowLabels = new Text[PerPage];
        readonly Image[] rowImages = new Image[PerPage];
        readonly List<OwnedWeapon> shown = new List<OwnedWeapon>();
        int page;

        public WeaponPanel(RectTransform parent, Action<OwnedWeapon> onPick, Action onMore, Action onClose)
        {
            var panel = Hud.NewImage("WeaponPanel", parent, null, new Color(0.08f, 0.09f, 0.11f, 0.94f), true);
            root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 1300f);
            rect.anchoredPosition = Vector2.zero;

            Label(rect, "Title", "Weapons", 44, new Vector2(0f, -50f), new Vector2(840f, 70f));
            pageLabel = Label(rect, "Page", string.Empty, 28, new Vector2(0f, -130f), new Vector2(840f, 60f));

            for (var i = 0; i < PerPage; i++)
            {
                var index = i;
                var button = Button(rect, "Weapon" + i, string.Empty, new Vector2(0f, -210f - i * (RowHeight + 10f)), new Vector2(820f, RowHeight), () =>
                {
                    if (index < shown.Count)
                    {
                        onPick(shown[index]);
                    }
                });
                rows[i] = button.gameObject;
                rowImages[i] = button.GetComponent<Image>();
                rowLabels[i] = button.GetComponentInChildren<Text>();
                rowLabels[i].fontSize = 30;
                rows[i].SetActive(false);
            }

            Button(rect, "More", "More", new Vector2(-215f, -1210f), new Vector2(400f, 80f), () =>
            {
                page++;
                onMore?.Invoke();
            });
            Button(rect, "Close", "Close", new Vector2(215f, -1210f), new Vector2(400f, 80f), onClose);
            root.SetActive(false);
        }

        public bool IsOpen => root.activeSelf;

        public int Page => page;

        public void SetOpen(bool open)
        {
            root.SetActive(open);
            if (open)
            {
                page = 0;
            }
        }

        public void Show(PlayerProfile profile, MasteryRules rules)
        {
            var pages = Math.Max(1, (profile.Weapons.Count + PerPage - 1) / PerPage);
            page %= pages;
            pageLabel.text = $"{profile.Weapons.Count} of {PlayerProfile.MaxWeapons} weapons   page {page + 1}/{pages}";
            shown.Clear();
            for (var i = 0; i < PerPage; i++)
            {
                var index = page * PerPage + i;
                var weapon = index < profile.Weapons.Count ? profile.Weapons[index] : null;
                rows[i].SetActive(weapon != null);
                if (weapon == null)
                {
                    continue;
                }

                shown.Add(weapon);
                var equipped = weapon.Uid == profile.EquippedUid;
                rowLabels[i].text = Describe(weapon, profile, rules, equipped);
                rowImages[i].color = equipped ? new Color(0.30f, 0.62f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);
            }
        }

        public static string Describe(OwnedWeapon weapon, PlayerProfile profile, MasteryRules rules, bool equipped)
        {
            var level = profile.MasteryFor(weapon.Type).Level(rules);
            return $"{(equipped ? "[in hand] " : string.Empty)}{MasteryProgress.Describe(weapon)}   mastery {level}";
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
