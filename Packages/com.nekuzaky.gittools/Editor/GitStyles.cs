using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>Shared colours and lazily built GUIStyles for the Git windows.</summary>
    public static class GitStyles
    {
        static bool Pro { get { return EditorGUIUtility.isProSkin; } }

        // ------------------------------------------------------------- colours

        public static Color PanelBackground { get { return Pro ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.78f, 0.78f, 0.78f); } }
        public static Color HeaderBackground { get { return Pro ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.72f, 0.72f, 0.72f); } }
        public static Color RowAlternate { get { return Pro ? new Color(1f, 1f, 1f, 0.02f) : new Color(0f, 0f, 0f, 0.02f); } }
        public static Color RowSelected { get { return Pro ? new Color(0.24f, 0.37f, 0.59f) : new Color(0.24f, 0.48f, 0.90f, 0.45f); } }
        public static Color RowHover { get { return Pro ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.05f); } }
        public static Color Splitter { get { return Pro ? new Color(0.11f, 0.11f, 0.11f) : new Color(0.6f, 0.6f, 0.6f); } }
        public static Color Muted { get { return Pro ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.35f, 0.35f, 0.35f); } }

        public static Color DiffAdded { get { return Pro ? new Color(0.16f, 0.32f, 0.18f) : new Color(0.78f, 0.93f, 0.78f); } }
        public static Color DiffRemoved { get { return Pro ? new Color(0.36f, 0.17f, 0.17f) : new Color(0.97f, 0.80f, 0.80f); } }
        public static Color DiffHunk { get { return Pro ? new Color(0.18f, 0.26f, 0.34f) : new Color(0.80f, 0.87f, 0.95f); } }

        public static Color HeadBadge { get { return new Color(0.24f, 0.55f, 0.90f); } }
        public static Color LocalBranchBadge { get { return new Color(0.30f, 0.62f, 0.36f); } }
        public static Color RemoteBranchBadge { get { return new Color(0.55f, 0.45f, 0.72f); } }
        public static Color TagBadge { get { return new Color(0.82f, 0.62f, 0.24f); } }
        public static Color StashBadge { get { return new Color(0.70f, 0.42f, 0.30f); } }

        /// <summary>Lane colours for the commit graph, cycled by lane index.</summary>
        static readonly Color[] s_LaneColors =
        {
            new Color(0.32f, 0.62f, 0.90f),
            new Color(0.40f, 0.76f, 0.44f),
            new Color(0.90f, 0.60f, 0.28f),
            new Color(0.78f, 0.42f, 0.78f),
            new Color(0.90f, 0.40f, 0.42f),
            new Color(0.36f, 0.78f, 0.76f),
            new Color(0.84f, 0.78f, 0.34f),
            new Color(0.58f, 0.55f, 0.86f),
        };

        public static Color LaneColor(int lane)
        {
            if (lane < 0) lane = 0;
            return s_LaneColors[lane % s_LaneColors.Length];
        }

        // -------------------------------------------------------------- styles

        static GUIStyle s_Mono;
        static GUIStyle s_MonoSmall;
        static GUIStyle s_Row;
        static GUIStyle s_RowMuted;
        static GUIStyle s_RowRight;
        static GUIStyle s_Badge;
        static GUIStyle s_SectionHeader;
        static GUIStyle s_Title;

        public static GUIStyle Mono
        {
            get
            {
                if (s_Mono == null)
                {
                    s_Mono = new GUIStyle(EditorStyles.label)
                    {
                        font = MonoFont(11),
                        wordWrap = false,
                        richText = false,
                        padding = new RectOffset(4, 4, 1, 1),
                        alignment = TextAnchor.MiddleLeft,
                    };
                    s_Mono.normal.textColor = EditorStyles.label.normal.textColor;
                }
                return s_Mono;
            }
        }

        public static GUIStyle MonoSmall
        {
            get
            {
                if (s_MonoSmall == null)
                {
                    s_MonoSmall = new GUIStyle(Mono) { fontSize = 10 };
                    s_MonoSmall.normal.textColor = Muted;
                }
                return s_MonoSmall;
            }
        }

        public static GUIStyle Row
        {
            get
            {
                if (s_Row == null)
                {
                    s_Row = new GUIStyle(EditorStyles.label)
                    {
                        padding = new RectOffset(4, 4, 0, 0),
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                    };
                }
                return s_Row;
            }
        }

        public static GUIStyle RowMuted
        {
            get
            {
                if (s_RowMuted == null)
                {
                    s_RowMuted = new GUIStyle(Row);
                    s_RowMuted.normal.textColor = Muted;
                }
                return s_RowMuted;
            }
        }

        public static GUIStyle RowRight
        {
            get
            {
                if (s_RowRight == null)
                {
                    s_RowRight = new GUIStyle(RowMuted) { alignment = TextAnchor.MiddleRight };
                }
                return s_RowRight;
            }
        }

        public static GUIStyle Badge
        {
            get
            {
                if (s_Badge == null)
                {
                    s_Badge = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(4, 4, 0, 0),
                        fontSize = 9,
                    };
                    s_Badge.normal.textColor = Color.white;
                }
                return s_Badge;
            }
        }

        public static GUIStyle SectionHeader
        {
            get
            {
                if (s_SectionHeader == null)
                {
                    s_SectionHeader = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 10,
                        padding = new RectOffset(4, 4, 0, 0),
                        alignment = TextAnchor.MiddleLeft,
                    };
                    s_SectionHeader.normal.textColor = Muted;
                }
                return s_SectionHeader;
            }
        }

        public static GUIStyle Title
        {
            get
            {
                if (s_Title == null)
                {
                    s_Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                }
                return s_Title;
            }
        }

        public static void Reset()
        {
            s_Mono = null;
            s_MonoSmall = null;
            s_Row = null;
            s_RowMuted = null;
            s_RowRight = null;
            s_Badge = null;
            s_SectionHeader = null;
            s_Title = null;
        }

        static Font MonoFont(int size)
        {
            return Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, size);
        }

        /// <summary>Draws a coloured pill with a label, returns the rect consumed.</summary>
        public static Rect DrawBadge(Rect rect, string label, Color color)
        {
            var width = Badge.CalcSize(new GUIContent(label)).x + 8f;
            var pill = new Rect(rect.x, rect.y + 2f, width, rect.height - 4f);
            EditorGUI.DrawRect(pill, color);
            GUI.Label(pill, label, Badge);
            return pill;
        }
    }
}
