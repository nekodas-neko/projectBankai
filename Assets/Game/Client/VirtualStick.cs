using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Client
{
    public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float DeadZone = 0.12f;
        const float IdleAlpha = 0.3f;

        RectTransform zone;
        RectTransform ring;
        RectTransform knob;
        CanvasGroup group;
        float radius;
        bool restOnRight;
        int activePointer = int.MinValue;

        public Vector2 Value { get; set; }

        public bool IsHeld => activePointer != int.MinValue;

        public void Initialize(RectTransform zone, RectTransform ring, RectTransform knob, CanvasGroup group, float radius, bool restOnRight)
        {
            this.zone = zone;
            this.ring = ring;
            this.knob = knob;
            this.group = group;
            this.radius = radius;
            this.restOnRight = restOnRight;
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld)
            {
                return;
            }

            activePointer = eventData.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out var local);
            var rect = zone.rect;
            ring.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x - rect.x, radius, Mathf.Max(radius, rect.width - radius)),
                Mathf.Clamp(local.y - rect.y, radius, Mathf.Max(radius, rect.height - radius)));
            group.alpha = 1f;
            Track(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer)
            {
                Track(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer)
            {
                Release();
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (zone != null && !IsHeld)
            {
                Release();
            }
        }

        void Track(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out var local);
            var offset = (local - new Vector2(zone.rect.x, zone.rect.y) - ring.anchoredPosition) / radius;
            offset = Vector2.ClampMagnitude(offset, 1f);
            knob.anchoredPosition = offset * radius;
            Value = offset.magnitude < DeadZone ? Vector2.zero : offset;
        }

        void Release()
        {
            activePointer = int.MinValue;
            Value = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            group.alpha = IdleAlpha;
            var rect = zone.rect;
            ring.anchoredPosition = new Vector2(restOnRight ? rect.width - radius - 90f : radius + 90f, radius + 150f);
        }
    }
}
