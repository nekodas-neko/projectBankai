using UnityEngine;

namespace Game.Client
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int appliedScreen;

        void Update()
        {
            if (Screen.safeArea != applied || appliedScreen.x != Screen.width || appliedScreen.y != Screen.height)
            {
                Apply();
            }
        }

        void OnEnable()
        {
            Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (appliedScreen.x <= 0 || appliedScreen.y <= 0)
            {
                return;
            }

            var rect = (RectTransform)transform;
            var min = applied.position;
            var max = applied.position + applied.size;
            rect.anchorMin = new Vector2(min.x / appliedScreen.x, min.y / appliedScreen.y);
            rect.anchorMax = new Vector2(max.x / appliedScreen.x, max.y / appliedScreen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
