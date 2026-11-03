using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Game.Core.EntityId;

namespace Game.Client
{
    // The whole dungeon in a corner of the screen: the zones, the gates (gold while closed), and a dot for everyone alive.
    public sealed class MiniMap
    {
        public static readonly Color ZoneColor = new Color(0.25f, 0.4f, 0.22f, 0.9f);
        public static readonly Color ClosedGateColor = new Color(1f, 0.8f, 0.25f, 1f);
        public static readonly Color OpenGateColor = new Color(0.55f, 0.46f, 0.32f, 0.9f);
        public static readonly Color LocalColor = Color.white;
        public static readonly Color AllyColor = new Color(0.35f, 0.7f, 1f, 1f);
        public static readonly Color EnemyColor = new Color(0.95f, 0.55f, 0.15f, 1f);
        public static readonly Color BossColor = new Color(0.95f, 0.15f, 0.15f, 1f);

        const float Width = 130f;
        const float Height = 300f;
        const float Pad = 8f;

        readonly RectTransform content;
        readonly GameObject root;
        readonly List<Image> gates = new List<Image>();
        readonly List<Image> dots = new List<Image>();
        WorldMap map;
        float scale;
        Vector2 offset;

        MiniMap(GameObject root, RectTransform content)
        {
            this.root = root;
            this.content = content;
        }

        public bool IsVisible => root.activeSelf;

        public int VisibleDots { get; private set; }

        public float Scale => scale;

        public static MiniMap Build(RectTransform parent)
        {
            var background = Hud.NewImage("MiniMap", parent, null, new Color(0f, 0f, 0f, 0.5f), false);
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = new Vector2(40f, -120f);

            var content = Hud.NewRect("Content", rect);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            content.pivot = Vector2.zero;

            var map = new MiniMap(background.gameObject, content);
            map.Hide();
            return map;
        }

        public void Hide()
        {
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        // Lays the zones out to fit the box. Call again if the map changes.
        public void Setup(WorldMap newMap)
        {
            map = newMap;
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }

            gates.Clear();
            dots.Clear();
            if (map == null)
            {
                Hide();
                return;
            }

            var width = map.MaxX - map.MinX;
            var height = map.MaxY - map.MinY;
            scale = Mathf.Min((Width - 2f * Pad) / width, (Height - 2f * Pad) / height);
            offset = new Vector2((Width - width * scale) * 0.5f, (Height - height * scale) * 0.5f);

            for (var i = 0; i < map.Zones.Count; i++)
            {
                var zone = map.Zones[i];
                Rectangle($"Zone {zone.Name}", zone.MinX, zone.MinY, zone.MaxX, zone.MaxY, ZoneColor);
            }

            for (var i = 0; i < map.Gates.Count; i++)
            {
                map.GateSpan(i, out var minX, out var minY, out var maxX, out var maxY);
                gates.Add(Rectangle($"Gate {map.Gates[i].Name}", minX, minY, maxX, maxY, ClosedGateColor));
            }

            root.SetActive(true);
        }

        public Vector2 PointFor(Vec2 world)
        {
            return new Vector2(offset.x + (world.X - map.MinX) * scale, offset.y + (world.Y - map.MinY) * scale);
        }

        public void Refresh(Snapshot snapshot, EntityId local)
        {
            if (map == null || snapshot == null)
            {
                return;
            }

            for (var i = 0; i < gates.Count; i++)
            {
                gates[i].color = map.IsOpen(i) ? OpenGateColor : ClosedGateColor;
            }

            var used = 0;
            for (var i = 0; i < snapshot.Entities.Count; i++)
            {
                var entity = snapshot.Entities[i];
                if (entity.Health <= 0f)
                {
                    continue;
                }

                var isLocal = entity.Id == local;
                var boss = (entity.Effects & EffectFlags.Boss) != 0;
                var size = isLocal ? 9f : boss ? 12f : entity.Owner.IsValid ? 8f : 6f;
                var color = isLocal ? LocalColor : boss ? BossColor : entity.Owner.IsValid ? AllyColor : EnemyColor;
                var dot = Dot(used++);
                dot.color = color;
                dot.rectTransform.sizeDelta = new Vector2(size, size);
                dot.rectTransform.anchoredPosition = PointFor(entity.Position);
            }

            for (var i = used; i < dots.Count; i++)
            {
                dots[i].gameObject.SetActive(false);
            }

            VisibleDots = used;
        }

        Image Dot(int index)
        {
            while (index >= dots.Count)
            {
                var dot = Hud.NewImage("Dot", content, ProceduralSprites.Disc, Color.white, false);
                dot.rectTransform.anchorMin = Vector2.zero;
                dot.rectTransform.anchorMax = Vector2.zero;
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                dots.Add(dot);
            }

            var result = dots[index];
            if (!result.gameObject.activeSelf)
            {
                result.gameObject.SetActive(true);
            }

            return result;
        }

        Image Rectangle(string name, float minX, float minY, float maxX, float maxY, Color color)
        {
            var image = Hud.NewImage(name, content, null, color, false);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = PointFor(new Vec2(minX, minY));
            rect.sizeDelta = new Vector2((maxX - minX) * scale, (maxY - minY) * scale);
            return image;
        }
    }
}
