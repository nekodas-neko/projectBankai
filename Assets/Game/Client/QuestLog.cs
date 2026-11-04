using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client
{
    public enum QuestState
    {
        Done,
        Active,
        Locked,
    }

    public readonly struct QuestEntry
    {
        public QuestEntry(string text, QuestState state)
        {
            Text = text;
            State = state;
        }

        public string Text { get; }

        public QuestState State { get; }
    }

    // The way through the dungeon as a short list: one step per section (kill its boss, or clear it when it has none).
    // Done steps are ticked, the one in front of the party is the current task, the rest are greyed out until it is done.
    public static class QuestLog
    {
        // Built from the dungeon data and the snapshot's room number: a section before the current one is behind the party.
        public static List<QuestEntry> Build(DungeonDef dungeon, Snapshot snapshot)
        {
            var entries = new List<QuestEntry>();
            if (dungeon == null || dungeon.Rooms.Length == 0)
            {
                return entries;
            }

            var running = snapshot != null && snapshot.Dungeon != DungeonPhase.None;
            var won = snapshot != null && snapshot.Dungeon == DungeonPhase.Victory;
            var current = running ? snapshot.Room - 1 : 0;
            for (var i = 0; i < dungeon.Rooms.Length; i++)
            {
                var state = won || i < current ? QuestState.Done : i == current ? QuestState.Active : QuestState.Locked;
                entries.Add(new QuestEntry(Step(dungeon.Rooms[i]), state));
            }

            return entries;
        }

        // The line for the top of the screen: the current task, or a closing line once the dungeon is won.
        public static string Current(List<QuestEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.State == QuestState.Active)
                {
                    return entry.Text;
                }
            }

            return entries.Count > 0 ? "Dungeon complete" : string.Empty;
        }

        public static string Step(RoomDef room)
        {
            foreach (var spawn in room.Spawns)
            {
                if (spawn.Boss)
                {
                    return "Defeat the " + Title(spawn.Class);
                }
            }

            return string.IsNullOrEmpty(room.Name) ? "Clear the area" : "Clear " + room.Name;
        }

        // "brood_mother" becomes "Brood Mother".
        public static string Title(string classId)
        {
            if (string.IsNullOrEmpty(classId))
            {
                return string.Empty;
            }

            var words = classId.Split('_');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
        }

        // The log as one block of rich text, in the order of the dungeon.
        public static string Format(List<QuestEntry> entries)
        {
            var text = new StringBuilder();
            foreach (var entry in entries)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                switch (entry.State)
                {
                    case QuestState.Done:
                        text.Append("<color=#7bd88f>[x] ").Append(entry.Text).Append("</color>");
                        break;
                    case QuestState.Active:
                        text.Append("<b>> ").Append(entry.Text).Append("</b>");
                        break;
                    default:
                        text.Append("<color=#7d7d7d>[ ] ").Append(entry.Text).Append("</color>");
                        break;
                }
            }

            return text.ToString();
        }
    }

    // The log on the right-hand side of the screen.
    public sealed class QuestLogPanel
    {
        const float Width = 320f;
        const float LineHeight = 40f;
        const float Header = 56f;

        readonly RectTransform root;
        readonly Text body;
        string shown = string.Empty;

        QuestLogPanel(RectTransform root, Text body)
        {
            this.root = root;
            this.body = body;
        }

        public string Text => body.text;

        public static QuestLogPanel Build(RectTransform parent)
        {
            var background = Hud.NewImage("QuestLog", parent, null, new Color(0f, 0f, 0f, 0.45f), false);
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(Width, Header);
            rect.anchoredPosition = new Vector2(-6f, -280f);

            var title = Hud.NewRect("Title", rect).gameObject.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 26;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.UpperLeft;
            title.color = new Color(1f, 0.85f, 0.4f);
            title.raycastTarget = false;
            title.text = "Dungeon log";
            title.rectTransform.offsetMin = new Vector2(12f, 0f);
            title.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var lines = Hud.NewRect("Lines", rect).gameObject.AddComponent<Text>();
            lines.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lines.fontSize = 24;
            lines.alignment = TextAnchor.UpperLeft;
            lines.color = Color.white;
            lines.supportRichText = true;
            lines.horizontalOverflow = HorizontalWrapMode.Wrap;
            lines.raycastTarget = false;
            lines.rectTransform.offsetMin = new Vector2(12f, 0f);
            lines.rectTransform.offsetMax = new Vector2(-8f, -Header + 6f);

            var panel = new QuestLogPanel(rect, lines);
            panel.Show(new List<QuestEntry>());
            return panel;
        }

        // Rewrites the list; does nothing when the text is the same, so it can be called every frame.
        public void Show(List<QuestEntry> entries)
        {
            var text = QuestLog.Format(entries);
            if (text == shown && root.gameObject.activeSelf == (entries.Count > 0))
            {
                return;
            }

            shown = text;
            body.text = text;
            root.gameObject.SetActive(entries.Count > 0);
            root.sizeDelta = new Vector2(Width, Header + LineHeight * entries.Count);
        }
    }
}
