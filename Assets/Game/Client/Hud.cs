using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client
{
    public static class HudSettings
    {
        const string MoveOnRightKey = "hud.moveOnRight.v2";

        const string CameraZoomKey = "hud.cameraZoom";
        const string SkinKey = "player.skin";
        const string ClassKey = "player.class";
        const string ModeKey = "session.mode";
        const string JoinKey = "session.joinAddress";
        const string StyleKey = "player.style";
        public const float MinZoom = 0.6f;
        public const float MaxZoom = 1.6f;
        public const float ZoomStep = 0.15f;

        public static bool MoveOnRight
        {
            get => PlayerPrefs.GetInt(MoveOnRightKey, 0) == 1;
            set => PlayerPrefs.SetInt(MoveOnRightKey, value ? 1 : 0);
        }

        public const string DefaultWeapon = "staff";

        public static string ClassName
        {
            get => Migrate(PlayerPrefs.GetString(ClassKey, DefaultWeapon));
            set => PlayerPrefs.SetString(ClassKey, value);
        }

        // Saved choices from before the kits were named for their weapons.
        public static string Migrate(string saved)
        {
            switch (saved)
            {
                case "tank": return "sword_and_shield";
                case "healer": return "healing_mace";
                case "mage": return "staff";
                default: return saved;
            }
        }

        public static string SkinFor(string classId)
        {
            var skins = SkinCatalog.ForClass(classId);
            var saved = PlayerPrefs.GetString(SkinKey + "." + classId, string.Empty);
            for (var i = 0; i < skins.Count; i++)
            {
                if (skins[i].name == saved)
                {
                    return saved;
                }
            }

            return skins.Count > 0 ? skins[0].name : SkinCatalog.DefaultSkin;
        }

        public static void SetSkin(string classId, string skinName)
        {
            PlayerPrefs.SetString(SkinKey + "." + classId, skinName);
        }

        public static int StyleFor(string classId)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(StyleKey + "." + classId, 0));
        }

        public static void SetStyle(string classId, int style)
        {
            PlayerPrefs.SetInt(StyleKey + "." + classId, style);
        }

        public static int SessionMode
        {
            get => PlayerPrefs.GetInt(ModeKey, 0);
            set => PlayerPrefs.SetInt(ModeKey, value);
        }

        public static string JoinAddress
        {
            get => PlayerPrefs.GetString(JoinKey, string.Empty);
            set => PlayerPrefs.SetString(JoinKey, value);
        }

        public static float CameraZoom
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(CameraZoomKey, 1f), MinZoom, MaxZoom);
            set => PlayerPrefs.SetFloat(CameraZoomKey, Mathf.Clamp(value, MinZoom, MaxZoom));
        }
    }

    public sealed class HudCallbacks
    {
        public Action SwapSides;
        public Action<int> Zoom;
        public Action CycleSkin;
        public Action CycleClass;
        public Action CycleStyle;
        public Action ToggleSession;
        public Action Restart;
    }

    public sealed class Hud
    {
        const float StickRadius = 130f;
        const float KnobSize = 120f;

        Hud(GameObject root, RectTransform safeArea, VirtualStick moveStick, ActionButton attack, ActionButton special, ActionButton ultimate, ActionButton defense, ResourceBar resource, DungeonBanner banner, BossFrame bossFrame, MiniMap miniMap, bool moveOnRight)
        {
            Root = root;
            SafeArea = safeArea;
            MoveStick = moveStick;
            Attack = attack;
            Special = special;
            Ultimate = ultimate;
            Defense = defense;
            Resource = resource;
            Banner = banner;
            BossFrame = bossFrame;
            MiniMap = miniMap;
            MoveOnRight = moveOnRight;
        }

        public GameObject Root { get; }

        public RectTransform SafeArea { get; }

        public VirtualStick MoveStick { get; }

        public ActionButton Attack { get; }

        public ActionButton Special { get; }

        public ActionButton Ultimate { get; }

        public ActionButton Defense { get; }

        public ResourceBar Resource { get; }

        public DungeonBanner Banner { get; }

        public BossFrame BossFrame { get; }

        public MiniMap MiniMap { get; }

        public bool MoveOnRight { get; }

        public static Hud Create(bool moveOnRight, HudCallbacks callbacks)
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
            BuildTextButton(safeArea, "SwapSides", "Swap sides", new Vector2(240f, 90f), new Vector2(-40f, -60f), callbacks.SwapSides);
            BuildTextButton(safeArea, "ZoomOut", "Zoom -", new Vector2(115f, 90f), new Vector2(-165f, -170f), () => callbacks.Zoom?.Invoke(-1));
            BuildTextButton(safeArea, "Skin", "Skin", new Vector2(115f, 90f), new Vector2(-165f, -280f), callbacks.CycleSkin);
            BuildTextButton(safeArea, "Class", "Weapon", new Vector2(115f, 90f), new Vector2(-40f, -280f), callbacks.CycleClass);
            BuildTextButton(safeArea, "ZoomIn", "Zoom +", new Vector2(115f, 90f), new Vector2(-40f, -170f), () => callbacks.Zoom?.Invoke(1));
            BuildTextButton(safeArea, "Session", "Session", new Vector2(240f, 90f), new Vector2(-40f, -390f), callbacks.ToggleSession);
            BuildTextButton(safeArea, "Style", "Style", new Vector2(240f, 90f), new Vector2(-40f, -500f), callbacks.CycleStyle);
            var attack = BuildActionButton(safeArea, moveOnRight, "Attack", new Vector2(190f, 290f), 100f, new Color(0.22f, 0.54f, 0.87f, 0.9f), true);
            var special = BuildActionButton(safeArea, moveOnRight, "Skill", new Vector2(370f, 380f), 72f, new Color(0.50f, 0.47f, 0.87f, 0.9f), false);
            var ultimate = BuildActionButton(safeArea, moveOnRight, "Ult", new Vector2(350f, 560f), 76f, new Color(0.93f, 0.62f, 0.18f, 0.9f), false);
            var defense = BuildActionButton(safeArea, moveOnRight, "Defend", new Vector2(150f, 500f), 68f, new Color(0.30f, 0.62f, 0.45f, 0.9f), false);
            var resource = ResourceBar.Build(safeArea);
            var banner = DungeonBanner.Build(safeArea, callbacks.Restart);
            var bossFrame = BossFrame.Build(safeArea);
            var miniMap = MiniMap.Build(safeArea);
            return new Hud(root, safeArea, stick, attack, special, ultimate, defense, resource, banner, bossFrame, miniMap, moveOnRight);
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

        static ActionButton BuildActionButton(RectTransform parent, bool moveOnRight, string text, Vector2 offset, float radius, Color color, bool repeat)
        {
            var onLeft = moveOnRight;
            var image = NewImage(text, parent, ProceduralSprites.Disc, color, true);
            var rect = image.rectTransform;
            var corner = new Vector2(onLeft ? 0f : 1f, 0f);
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            rect.anchoredPosition = new Vector2(onLeft ? offset.x : -offset.x, offset.y);

            var fill = NewImage("Cooldown", rect, ProceduralSprites.Disc, new Color(0f, 0f, 0f, 0.6f), false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            fill.fillAmount = 0f;

            var label = NewRect("Label", rect).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = radius > 80f ? 38 : 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;

            var button = rect.gameObject.AddComponent<ActionButton>();
            button.Initialize(fill, label);
            button.RepeatWhileHeld = repeat;
            return button;
        }

        static void BuildTextButton(RectTransform parent, string name, string text, Vector2 size, Vector2 position, Action onClick)
        {
            var image = NewImage(name, parent, null, new Color(1f, 1f, 1f, 0.15f), true);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var label = NewRect("Label", rect).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size.x > 200f ? 34 : 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            label.raycastTarget = false;
            label.text = text;
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
