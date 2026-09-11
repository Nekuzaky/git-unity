using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>
    /// Unicode glyphs used across the UI, emoji included.
    ///
    /// Two constraints shape this set.
    ///
    /// Emoji render here as monochrome silhouettes, never in colour: IMGUI rasterises text
    /// through a dynamic font and ignores the COLR/CPAL layers, falling back to the base
    /// outline. Segoe UI Symbol and Segoe UI Emoji both carry those outlines, so ☕, ⚠, ✏,
    /// ⬆ and ⬇ below come out as proper shapes.
    ///
    /// Everything must live in the Basic Multilingual Plane. IMGUI addresses glyphs per
    /// UTF-16 code unit (Font.HasCharacter takes a char), so an astral emoji such as 🌿
    /// (U+1F33F) is looked up as two separate surrogates and renders as two blanks, even
    /// though the fonts do contain the glyph.
    ///
    /// Swap any entry here and the whole UI follows.
    /// </summary>
    public static class GitIcons
    {
        public const string Refresh = "↻";  // ↻
        public const string Fetch = "⇅";    // ⇅
        public const string Pull = "⬇";     // emoji down
        public const string Push = "⬆";     // emoji up
        public const string Branch = "◆";   // ◆
        public const string Remote = "◇";   // ◇
        public const string Tag = "⚑";      // ⚑
        public const string Stash = "▤";    // ▤
        public const string Changes = "✏";  // emoji pencil
        public const string Current = "●";  // ●
        public const string Other = "○";    // ○
        public const string Merge = "◈";    // ◈
        public const string Expanded = "▾"; // ▾
        public const string Collapsed = "▸";// ▸
        public const string Ahead = "↑";    // ↑
        public const string Behind = "↓";   // ↓
        public const string Conflict = "⚠"; // emoji warning
        public const string Discard = "✕";  // ✕
        public const string Stage = "+";
        public const string Unstage = "−";  // −
        public const string Theme = "◐";    // ◐
        public const string Console = "≡";  // ≡
        public const string Arrow = "→";    // →
        public const string Sidebar = "☰";  // ☰
        public const string More = "⋯";     // ⋯
        public const string Coffee = "☕";   // ☕
        public const string Empty = "▫";    // ▫
        public const string Separator = "·"; // ·
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
        public static Color FooterBackground { get { return Dark ? Hex(0x14171B) : Hex(0xB8B8B8); } }
        public static Color Border { get { return Dark ? Hex(0x0E1114) : Hex(0xA5A5A5); } }

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
        static GUIStyle s_Footer;
        static GUIStyle s_Link;
        static GUIStyle s_EmptyState;
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
                        font = MonoFont(12),
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
                    s_MonoSmall = new GUIStyle(Mono) { fontSize = 11 };
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
                        font = SymbolFont(13),
                        fontSize = 13,
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
                    s_ToolbarButton = new GUIStyle(EditorStyles.toolbarButton) { font = SymbolFont(12), fontSize = 12 };
                }
                return s_ToolbarButton;
            }
        }

        /// <summary>Muted footer text.</summary>
        public static GUIStyle Footer
        {
            get
            {
                EnsureTheme();
                if (s_Footer == null)
                {
                    s_Footer = new GUIStyle(EditorStyles.miniLabel)
                    {
                        font = SymbolFont(11),
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(10, 10, 0, 0),
                    };
                    s_Footer.normal.textColor = Muted;
                }
                return s_Footer;
            }
        }

        /// <summary>Clickable footer link: accent coloured, right aligned.</summary>
        public static GUIStyle Link
        {
            get
            {
                EnsureTheme();
                if (s_Link == null)
                {
                    s_Link = new GUIStyle(Footer) { alignment = TextAnchor.MiddleCenter };
                    s_Link.normal.textColor = Text;
                    s_Link.hover.textColor = Accent;
                }
                return s_Link;
            }
        }

        /// <summary>Centred message for an empty list.</summary>
        public static GUIStyle EmptyState
        {
            get
            {
                EnsureTheme();
                if (s_EmptyState == null)
                {
                    s_EmptyState = new GUIStyle(EditorStyles.label)
                    {
                        font = SymbolFont(12),
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                    };
                    s_EmptyState.normal.textColor = Muted;
                }
                return s_EmptyState;
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
                        fontSize = 10,
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
                        font = SymbolFont(11),
                        fontSize = 11,
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
                    s_Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
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
            s_Footer = null;
            s_Link = null;
            s_EmptyState = null;
            s_SectionHeader = null;
            s_Title = null;
            s_Subtitle = null;
        }

        // Fonts are cached per size and deliberately survive Reset(): rebuilding the styles
        // on a theme change must not strand a set of dynamic fonts that nothing destroys.
        static readonly Dictionary<int, Font> s_SymbolFonts = new Dictionary<int, Font>();
        static readonly Dictionary<int, Font> s_MonoFonts = new Dictionary<int, Font>();

        static readonly string[] SymbolFamilies =
        {
            "Segoe UI Symbol", "Segoe UI Emoji", "Apple Symbols", "Apple Color Emoji",
            "Noto Emoji", "Arial Unicode MS", "DejaVu Sans", "Segoe UI", "Arial",
        };

        static readonly string[] MonoFamilies = { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" };

        /// <summary>Font that covers both Latin text and the GitIcons glyphs.</summary>
        static Font SymbolFont(int size)
        {
            return CachedFont(s_SymbolFonts, SymbolFamilies, size);
        }

        static Font MonoFont(int size)
        {
            return CachedFont(s_MonoFonts, MonoFamilies, size);
        }

        static Font CachedFont(Dictionary<int, Font> cache, string[] families, int size)
        {
            Font font;
            // The null test also catches a Font destroyed by a domain reload.
            if (cache.TryGetValue(size, out font) && font != null) return font;

            font = Font.CreateDynamicFontFromOSFont(families, size);
            cache[size] = font;
            return font;
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

        /// <summary>
        /// Draws an icon tinted with <paramref name="color"/>: Bootstrap artwork when the
        /// vector graphics module is available, the Unicode glyph otherwise.
        /// </summary>
        public static void DrawIcon(Rect rect, string glyph, Color color, string slot = null)
        {
            var previous = GUI.color;
            GUI.color = color;

            var texture = slot != null ? GitIconTextures.Get(slot, IconPixelSize) : null;
            if (texture != null)
            {
                var size = Mathf.Min(IconPixelSize, Mathf.Min(rect.width, rect.height));
                var box = new Rect(
                    rect.x + (rect.width - size) * 0.5f,
                    rect.y + (rect.height - size) * 0.5f,
                    size, size);

                GUI.DrawTexture(box, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(rect, glyph, Icon);
            }

            GUI.color = previous;
        }

        /// <summary>Pixel size the icon artwork is rasterised at.</summary>
        public const int IconPixelSize = 16;

        /// <summary>
        /// Label for a control that carries an icon: artwork plus text where the artwork is
        /// available, the glyph prefixed to the text otherwise.
        /// </summary>
        public static GUIContent IconLabel(string slot, string glyph, string text, string tooltip = null)
        {
            var texture = GitIconTextures.Get(slot, IconPixelSize);
            if (texture != null)
                return string.IsNullOrEmpty(text)
                    ? new GUIContent(texture, tooltip ?? text)
                    : new GUIContent("  " + text, texture, tooltip ?? text);

            return string.IsNullOrEmpty(text)
                ? new GUIContent(glyph, tooltip)
                : new GUIContent(glyph + "  " + text, tooltip ?? text);
        }

        /// <summary>Hairline along the bottom edge of a rect, used under pane headers.</summary>
        public static void DrawBottomBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
        }

        /// <summary>Highlights a row and marks it with a left accent bar, the way Fork does.</summary>
        public static void DrawSelectedRow(Rect rect)
        {
            EditorGUI.DrawRect(rect, RowSelected);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), Accent);
        }
    }
}
