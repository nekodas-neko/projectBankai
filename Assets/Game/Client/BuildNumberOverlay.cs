using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public class BuildNumberOverlay : MonoBehaviour
    {
        const int FontSize = 28;
        const float Margin = 12f;

        RectTransform safeArea;
        Rect appliedSafeArea;
        Vector2Int appliedScreen;
        int shownTuning = -1;
        string shownExtra = string.Empty;

        // One more line above the build number, for things worth seeing while testing (the weapon mastery). Empty hides it.
        public static string Extra { get; set; } = string.Empty;

        public Text Label { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<BuildNumberOverlay>() != null)
            {
                return;
            }

            var root = new GameObject("BuildNumberOverlay");
            DontDestroyOnLoad(root);
            root.AddComponent<BuildNumberOverlay>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var areaObject = new GameObject("SafeArea", typeof(RectTransform));
            safeArea = (RectTransform)areaObject.transform;
            safeArea.SetParent(transform, false);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(safeArea, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(Margin, Margin);
            labelRect.offsetMax = new Vector2(-Margin, -Margin);

            Label = labelObject.AddComponent<Text>();
            Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label.fontSize = FontSize;
            Label.alignment = TextAnchor.LowerLeft;
            Label.color = new Color(1f, 1f, 1f, 0.85f);
            Label.raycastTarget = false;
            RefreshText();

            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            ApplySafeArea();
        }

        void Update()
        {
            if (shownTuning != DefinitionLoader.TuningChanges || shownExtra != Extra)
            {
                RefreshText();
            }

            if (Screen.safeArea != appliedSafeArea || appliedScreen.x != Screen.width || appliedScreen.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        void RefreshText()
        {
            shownTuning = DefinitionLoader.TuningChanges;
            shownExtra = Extra;
            var status = DefinitionLoader.TuningStatus;
            var text = status.Length > 0 ? BuildInfo.Version + "\n" + status : BuildInfo.Version;
            Label.text = shownExtra.Length > 0 ? shownExtra + "\n" + text : text;
        }

        void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);

            if (appliedScreen.x <= 0 || appliedScreen.y <= 0)
            {
                return;
            }

            var min = appliedSafeArea.position;
            var max = appliedSafeArea.position + appliedSafeArea.size;
            safeArea.anchorMin = new Vector2(min.x / appliedScreen.x, min.y / appliedScreen.y);
            safeArea.anchorMax = new Vector2(max.x / appliedScreen.x, max.y / appliedScreen.y);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }
    }
}
