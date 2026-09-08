using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>
    /// Colourised, virtualised diff renderer. Only the lines inside the viewport are
    /// drawn, so a several-thousand-line diff still scrolls smoothly.
    /// </summary>
    public class GitDiffView
    {
        const float LineHeight = 15f;

        string[] m_Lines = new string[0];
        Vector2 m_Scroll;
        float m_ContentWidth = 400f;
        string m_Placeholder = "";

        public int LineCount { get { return m_Lines.Length; } }

        public void Clear(string placeholder)
        {
            m_Lines = new string[0];
            m_Placeholder = placeholder;
            m_Scroll = Vector2.zero;
        }

        public void SetText(string diff)
        {
            m_Placeholder = "";
            m_Scroll = Vector2.zero;

            if (string.IsNullOrEmpty(diff))
            {
                m_Lines = new string[0];
                m_Placeholder = "(no textual difference)";
                return;
            }

            m_Lines = diff.Replace("\r\n", "\n").Split('\n');

            var longest = "";
            foreach (var line in m_Lines)
                if (line.Length > longest.Length) longest = line;

            // 400 chars is far past the point where horizontal scrolling stays useful.
            if (longest.Length > 400) longest = longest.Substring(0, 400);
            m_ContentWidth = GitStyles.Mono.CalcSize(new GUIContent(longest)).x + 24f;
        }

        public void Draw(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.PanelBackground);

            if (m_Lines.Length == 0)
            {
                var label = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 18f);
                GUI.Label(label, m_Placeholder, GitStyles.RowMuted);
                return;
            }

            var content = new Rect(0f, 0f, Mathf.Max(m_ContentWidth, rect.width - 16f), m_Lines.Length * LineHeight);
            m_Scroll = GUI.BeginScrollView(rect, m_Scroll, content);

            int first = Mathf.Max(0, Mathf.FloorToInt(m_Scroll.y / LineHeight) - 1);
            int last = Mathf.Min(m_Lines.Length, first + Mathf.CeilToInt(rect.height / LineHeight) + 2);

            for (int i = first; i < last; i++)
            {
                var line = m_Lines[i];
                var row = new Rect(0f, i * LineHeight, content.width, LineHeight);

                var background = BackgroundFor(line);
                if (background.a > 0f) EditorGUI.DrawRect(row, background);

                GUI.Label(row, line, StyleFor(line));
            }

            GUI.EndScrollView();
        }

        static Color BackgroundFor(string line)
        {
            if (line.Length == 0) return Color.clear;
            if (line.StartsWith("+++") || line.StartsWith("---")) return Color.clear;
            if (line.StartsWith("@@")) return GitStyles.DiffHunk;
            if (line[0] == '+') return GitStyles.DiffAdded;
            if (line[0] == '-') return GitStyles.DiffRemoved;
            return Color.clear;
        }

        static GUIStyle StyleFor(string line)
        {
            if (line.StartsWith("diff --git") || line.StartsWith("index ") ||
                line.StartsWith("+++") || line.StartsWith("---") ||
                line.StartsWith("new file") || line.StartsWith("deleted file") ||
                line.StartsWith("similarity index") || line.StartsWith("rename "))
                return GitStyles.MonoSmall;

            return GitStyles.Mono;
        }
    }
}
