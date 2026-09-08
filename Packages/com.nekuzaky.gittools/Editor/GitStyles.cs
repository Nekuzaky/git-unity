using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>
    /// Unicode glyphs used across the UI.
    ///
    /// These are deliberately NOT colour emoji: Unity's IMGUI renders text through a dynamic
    /// font that has no COLR/CBDT support, so 🌿 or 🏷 come out as empty boxes on most
    /// setups. Every glyph below lives in the Basic Multilingual Plane and is covered by the
    /// editor font (or by the fallbacks declared in <see cref="GitStyles"/>), so they render
    /// as real shapes everywhere. Swap any of them here and the whole UI follows.
    /// </summary>
    public static class GitIcons
    {
        public const string Refresh = "↻";  // ↻
        public const string Fetch = "⇅";    // ⇅
        public const string Pull = "↓";     // ↓
        public const string Push = "↑";     // ↑
        public const string Branch = "◆";   // ◆
        public const string Remote = "◇";   // ◇
        public const string Tag = "⚑";      // ⚑
        public const string Stash = "▤";    // ▤
        public const string Changes = "✎";  // ✎
        public const string Current = "●";  // ●
        public const string Other = "○";    // ○
        public const string Merge = "◈";    // ◈
        public const string Expanded = "▾"; // ▾
        public const string Collapsed = "▸";// ▸
        public const string Ahead = "↑";    // ↑
        public const string Behind = "↓";   // ↓
        public const string Conflict = "✱"; // ✱
        public const string Discard = "✕";  // ✕
        public const string Stage = "+";
        public const string Unstage = "−";  // −
        public const string Theme = "◐";    // ◐
        public const string Console = "≡";  // ≡
        public const string Arrow = "→";    // →
    }

    /// <summary>
    /// Palette and lazily built GUIStyles.
    ///
    /// The window ships its own dark theme rather than inheriting the editor skin, so it
    /// reads like a dedicated Git client. Users on the light skin can switch it off, and the
    /// styles rebuild themselves when the theme changes.
    /// </summary>
    public static class GitStyles
    {
        const string PrefDark = "GitTools.DarkTheme";

        static bool s_Initialised;
        static bool s_Dark = true;
        static bool s_BuiltDark;

        /// <summary>Dark theme, on by default and remembered per user.</summary>
        public static bool Dark
        {
            get
            {
                if (!s_Initialised)
                {
                    s_Initialised = true;
                    s_Dark = EditorPrefs.GetBool(PrefDark, true);
                }
                return s_Dark;
            }
            set
            {
                s_Initialised = true;
                s_Dark = value;
                EditorPrefs.SetBool(PrefDark, value);
                Reset();
            }
        }

        // ------------------------------------------------------------- colours

        public static Color WindowBackground { get { return Dark ? Hex(0x16191D) : Hex(0xC8C8C8); } }
        public static Color PanelBackground { get { return Dark ? Hex(0x1E2227) : Hex(0xCFCFCF); } }
        public static Color HeaderBackground { get { return Dark ? Hex(0x282D34) : Hex(0xBDBDBD); } }
        public static Color RowAlternate { get { return Dark ? new Color(1f, 1f, 1f, 0.022f) : new Color(0f, 0f, 0f, 0.022f); } }
        public static Color RowSelected { get { return Dark ? Hex(0x2F5C8F) : new Color(0.24f, 0.48f, 0.90f, 0.45f); } }
        public static Color RowHover { get { return Dark ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.05f); } }
        public static Color Accent { get { return Hex(0x4C9AFF); } }
        public static Color Splitter { get { return Dark ? Hex(0x101316) : Hex(0x9A9A9A); } }
        public static Color Text { get { return Dark ? Hex(0xD6DAE0) : Hex(0x1E1E1E); } }
        public static Color Muted { get { return Dark ? Hex(0x8A929C) : Hex(0x585858); } }
        public static Color Danger { get { return Hex(0x8C2B2B); } }

        public static Color DiffAdded { get { return Dark ? Hex(0x15321C) : Hex(0xC6EDC6); } }
        public static Color DiffRemoved { get { return Dark ? Hex(0x3A1C1E) : Hex(0xF7CCCC); } }
        public static Color DiffHunk { get { return Dark ? Hex(0x1C2C3C) : Hex(0xCCDDF2); } }

        public static Color HeadBadge { get { return Hex(0x3D7EBF); } }
        public static Color LocalBranchBadge { get { return Hex(0x4C9E5A); } }
        public static Color RemoteBranchBadge { get { return Hex(0x8B72B8); } }
        public static Color TagBadge { get { return Hex(0xC79A3D); } }
        public static Color StashBadge { get { return Hex(0xB36B4C); } }

        public static Color StatusAdded { get { return Hex(0x4C9E5A); } }
        public static Color StatusModified { get { return Hex(0x3D7EBF); } }
        public static Color StatusDeleted { get { return Hex(0xC0504D); } }
        public static Color StatusRenamed { get { return Hex(0x8B72B8); } }
        public static Color StatusConflict { get { return Hex(0xE0742E); } }

        /// <summary>Lane colours for the commit graph, cycled by lane index.</summary>
        static readonly Color[] s_LaneColors =
        {
            Hex(0x4C9AFF), Hex(0x5FC26E), Hex(0xE8A33D), Hex(0xC77DD8),
            Hex(0xE8615F), Hex(0x4FC4C0), Hex(0xD9CF52), Hex(0x8F8AE0),
        };

        public static Color LaneColor(int lane)
        {
            if (lane < 0) lane = 0;
            return s_LaneColors[lane % s_LaneColors.Length];
        }

        static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
        }

        // -------------------------------------------------------------- styles

        static GUIStyle s_Mono;
        static GUIStyle s_MonoSmall;
        static GUIStyle s_Row;
        static GUIStyle s_RowBold;
        static GUIStyle s_RowMuted;
        static GUIStyle s_RowRight;
        static GUIStyle s_Badge;
        static GUIStyle s_Icon;
        static GUIStyle s_ToolbarButton;
        static GUIStyle s_SectionHeader;
        static GUIStyle s_Title;
        static GUIStyle s_Subtitle;

        static void EnsureTheme()
        {
            // The cached styles bake in the palette, so a theme flip has to drop them.
            if (s_BuiltDark != Dark)
            {
                Reset();
                s_BuiltDark = Dark;
            }
        }

        public static GUIStyle Mono
        {
            get
            {
                EnsureTheme();
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
                    s_Mono.normal.textColor = Text;
                }
                return s_Mono;
            }
        }

        public static GUIStyle MonoSmall
        {
            get
            {
                EnsureTheme();
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
                EnsureTheme();
                if (s_Row == null)
                {
                    s_Row = new GUIStyle(EditorStyles.label)
                    {
                        padding = new RectOffset(4, 4, 0, 0),
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                    };
                    s_Row.normal.textColor = Text;
                }
                return s_Row;
            }
        }

        public static GUIStyle RowBold
        {
            get
            {
                EnsureTheme();
                if (s_RowBold == null)
                {
                    s_RowBold = new GUIStyle(Row) { fontStyle = FontStyle.Bold };
                    s_RowBold.normal.textColor = Text;
                }
                return s_RowBold;
            }
        }

        public static GUIStyle RowMuted
        {
            get
            {
                EnsureTheme();
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
                EnsureTheme();
                if (s_RowRight == null) s_RowRight = new GUIStyle(RowMuted) { alignment = TextAnchor.MiddleRight };
                return s_RowRight;
            }
        }

        /// <summary>
        /// Style for the Unicode glyphs. The fallback chain matters: the editor font does not
        /// cover every symbol, and without these the glyph silently becomes a blank box.
        /// </summary>
        public static GUIStyle Icon
        {
            get
            {
                EnsureTheme();
                if (s_Icon == null)
                {
                    s_Icon = new GUIStyle(EditorStyles.label)
                    {
                        font = SymbolFont(11),
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(0, 0, 0, 0),
                    };
                    s_Icon.normal.textColor = Text;
                }
                return s_Icon;
            }
        }

        /// <summary>
        /// Toolbar button carrying a glyph. It must not use EditorStyles.toolbarButton as-is:
        /// that style renders with the editor font, which on Windows resolves to Segoe UI —
        /// a font missing 13 of the glyphs in <see cref="GitIcons"/>, so they would come out
        /// as empty boxes. Symbol-capable font first, and Latin still renders from it.
        /// </summary>
        public static GUIStyle ToolbarButton
        {
            get
            {
                EnsureTheme();
                if (s_ToolbarButton == null)
                {
                    s_ToolbarButton = new GUIStyle(EditorStyles.toolbarButton) { font = SymbolFont(11) };
                }
                return s_ToolbarButton;
            }
        }

        public static GUIStyle Badge
        {
            get
            {
                EnsureTheme();
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
                EnsureTheme();
                if (s_SectionHeader == null)
                {
                    s_SectionHeader = new GUIStyle(EditorStyles.boldLabel)
                    {
                        font = SymbolFont(10),
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
                EnsureTheme();
                if (s_Title == null)
                {
                    s_Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    s_Title.normal.textColor = Text;
                }
                return s_Title;
            }
        }

        public static GUIStyle Subtitle
        {
            get
            {
                EnsureTheme();
                if (s_Subtitle == null)
                {
                    s_Subtitle = new GUIStyle(EditorStyles.miniLabel);
                    s_Subtitle.normal.textColor = Muted;
                }
                return s_Subtitle;
            }
        }

        public static void Reset()
        {
            s_Mono = null;
            s_MonoSmall = null;
            s_Row = null;
            s_RowBold = null;
            s_RowMuted = null;
            s_RowRight = null;
            s_Badge = null;
            s_Icon = null;
            s_ToolbarButton = null;
            s_SectionHeader = null;
            s_Title = null;
            s_Subtitle = null;
        }

        /// <summary>Font that covers both Latin text and the GitIcons glyphs.</summary>
        static Font SymbolFont(int size)
        {
            return Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI Symbol", "Apple Symbols", "Arial Unicode MS", "DejaVu Sans", "Segoe UI", "Arial" }, size);
        }

        static Font MonoFont(int size)
        {
            return Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, size);
        }

        // ------------------------------------------------------------- drawing

        /// <summary>Draws a coloured pill with a label and returns the rect it used.</summary>
        public static Rect DrawBadge(Rect rect, string label, Color color)
        {
            var width = Badge.CalcSize(new GUIContent(label)).x + 8f;
            var pill = new Rect(rect.x, rect.y + 2f, width, rect.height - 4f);
            EditorGUI.DrawRect(pill, color);
            GUI.Label(pill, label, Badge);
            return pill;
        }

        /// <summary>Draws a small square carrying a status letter (A, M, D, R, U).</summary>
        public static void DrawStatusBadge(Rect rect, char status, Color color)
        {
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, status.ToString(), Badge);
        }

        /// <summary>Draws a Unicode glyph tinted with <paramref name="color"/>.</summary>
        public static void DrawIcon(Rect rect, string glyph, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, glyph, Icon);
            GUI.color = previous;
        }

        /// <summary>Highlights a row and marks it with a left accent bar, the way Fork does.</summary>
        public static void DrawSelectedRow(Rect rect)
        {
            EditorGUI.DrawRect(rect, RowSelected);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), Accent);
        }
    }
}
