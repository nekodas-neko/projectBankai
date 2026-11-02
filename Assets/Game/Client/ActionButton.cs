using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client
{
    public class ActionButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float DragThresholdScreenFraction = 0.03f;
        const float MaxDragScreenFraction = 0.14f;
        const float RepeatDelay = 0.25f;

        Image cooldownFill;
        Text label;
        int activePointer = int.MinValue;
        Vector2 downPosition;
        bool dragging;
        bool repeated;
        float heldSeconds;
        float nextRepeat;

        public event Action<Vector2> Fired;

        public bool RepeatWhileHeld { get; set; }

        public float RepeatInterval { get; set; } = 0.3f;

        public bool IsAiming => dragging;

        public Vector2 Aim { get; private set; }

        public void Initialize(Image cooldownFill, Text label)
        {
            this.cooldownFill = cooldownFill;
            this.label = label;
        }

        public void SetCooldown(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            cooldownFill.fillAmount = fraction;
            label.color = new Color(1f, 1f, 1f, fraction > 0f ? 0.5f : 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != int.MinValue)
            {
                return;
            }

            activePointer = eventData.pointerId;
            downPosition = eventData.position;
            dragging = false;
            repeated = false;
            heldSeconds = 0f;
            Aim = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer)
            {
                return;
            }

            var delta = eventData.position - downPosition;
            var threshold = Screen.width * DragThresholdScreenFraction;
            if (!dragging && delta.magnitude >= threshold)
            {
                dragging = true;
            }

            if (dragging)
            {
                Aim = Vector2.ClampMagnitude(delta / (Screen.width * MaxDragScreenFraction), 1f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer)
            {
                return;
            }

            var aim = dragging ? Aim : Vector2.zero;
            var fire = dragging || !repeated;
            activePointer = int.MinValue;
            dragging = false;
            Aim = Vector2.zero;

            if (fire)
            {
                Fired?.Invoke(aim);
            }
        }

        void Update()
        {
            if (activePointer == int.MinValue || dragging || !RepeatWhileHeld)
            {
                return;
            }

            heldSeconds += Time.unscaledDeltaTime;
            if (heldSeconds >= RepeatDelay && Time.unscaledTime >= nextRepeat)
            {
                repeated = true;
                nextRepeat = Time.unscaledTime + RepeatInterval;
                Fired?.Invoke(Vector2.zero);
            }
        }
    }
}
