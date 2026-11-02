using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client
{
    public static class HudSettings
    {
        const string MoveOnRightKey = "hud.moveOnRight";

        public static bool MoveOnRight
        {
            get => PlayerPrefs.GetInt(MoveOnRightKey, 1) == 1;
            set => PlayerPrefs.SetInt(MoveOnRightKey, value ? 1 : 0);
        }
    }

    public sealed class Hud
    {
        const float StickRadius = 130f;
        const float KnobSize = 120f;

        Hud(GameObject root, RectTransform safeArea, VirtualStick moveStick, bool moveOnRight)
        {
            Root = root;
            SafeArea = safeArea;
            MoveStick = moveStick;
            MoveOnRight = moveOnRight;
        }

        public GameObject Root { get; }

        public RectTransform SafeArea { get; }

        public VirtualStick MoveStick { get; }

        public bool MoveOnRight { get; }

        public static Hud Create(bool moveOnRight, Action onSwapSides)
        {
            var root = new GameObject("Hud");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            var safeArea = NewRect("SafeArea", root.transform);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var stick = BuildMoveStick(safeArea, moveOnRight);
            BuildSwapButton(safeArea, onSwapSides);
            return new Hud(root, safeArea, stick, moveOnRight);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool raycast)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static VirtualStick BuildMoveStick(RectTransform parent, bool moveOnRight)
        {
            var zoneImage = NewImage("MoveZone", parent, null, new Color(1f, 1f, 1f, 0f), true);
            var zone = zoneImage.rectTransform;
            zone.anchorMin = moveOnRight ? new Vector2(0.4f, 0f) : new Vector2(0f, 0f);
            zone.anchorMax = moveOnRight ? new Vector2(1f, 0.5f) : new Vector2(0.6f, 0.5f);

            var group = zone.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            var ring = NewImage("Ring", zone, ProceduralSprites.Ring, new Color(1f, 1f, 1f, 0.7f), false).rectTransform;
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.zero;
            ring.sizeDelta = new Vector2(StickRadius * 2f, StickRadius * 2f);

            var knob = NewImage("Knob", ring, ProceduralSprites.Disc, new Color(1f, 1f, 1f, 0.8f), false).rectTransform;
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(KnobSize, KnobSize);

            var stick = zone.gameObject.AddComponent<VirtualStick>();
            stick.Initialize(zone, ring, knob, group, StickRadius, moveOnRight);
            return stick;
        }

        static void BuildSwapButton(RectTransform parent, Action onSwapSides)
        {
            var image = NewImage("SwapSides", parent, null, new Color(1f, 1f, 1f, 0.15f), true);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(240f, 90f);
            rect.anchoredPosition = new Vector2(-40f, -60f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onSwapSides?.Invoke());

            var label = NewRect("Label", rect).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            label.raycastTarget = false;
            label.text = "Swap sides";
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
