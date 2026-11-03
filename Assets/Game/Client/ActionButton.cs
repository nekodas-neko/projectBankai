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
        const float PressedScale = 1.12f;

        Image cooldownFill;
        Text label;
        int activePointer = int.MinValue;
        Vector2 downPosition;
        bool dragging;
        bool repeated;
        float heldSeconds;
        float nextRepeat;

        public event Action<Vector2> Fired;

        // Fired instead of Fired when the button was held for ChargeSeconds before it was let go (only with HoldForHeavy).
        public event Action<Vector2> FiredHeavy;

        // A tap is the light version and holding is the heavy one; the button grows while it charges. Dragging to aim cancels the charge, so an aimed attack is always the light one.
        public bool HoldForHeavy { get; set; }

        public float ChargeSeconds { get; set; } = 0.4f;

        public float Charge => activePointer == int.MinValue || !HoldForHeavy || dragging ? 0f : Mathf.Clamp01(heldSeconds / ChargeSeconds);

        public bool IsCharged => Charge >= 1f;

        public bool RepeatWhileHeld { get; set; }

        public float RepeatInterval { get; set; } = 0.3f;

        public bool IsAiming => dragging;

        public Vector2 Aim { get; private set; }

        public void Initialize(Image cooldownFill, Text label)
        {
            this.cooldownFill = cooldownFill;
            this.label = label;
        }

        public void SetLabel(string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        // How much of the dark ring still covers the button: the cooldown left, or how far the weapon's resource
        // is from the cost, whichever is more. A cost of zero or less (an attack that gives resource) never darkens it.
        public static float RingFill(float cooldownFraction, float resource, float maxResource, float cost)
        {
            var missing = cost > 0f && maxResource > 0f ? 1f - Mathf.Clamp01(resource / cost) : 0f;
            return Mathf.Clamp01(Mathf.Max(cooldownFraction, missing));
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
            var heavy = IsCharged;
            activePointer = int.MinValue;
            dragging = false;
            Aim = Vector2.zero;
            transform.localScale = Vector3.one;

            if (heavy)
            {
                FiredHeavy?.Invoke(aim);
            }
            else if (fire)
            {
                Fired?.Invoke(aim);
            }
        }

        void Update()
        {
            if (activePointer != int.MinValue && HoldForHeavy)
            {
                heldSeconds += Time.unscaledDeltaTime;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, PressedScale, Charge);
                return;
            }

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
