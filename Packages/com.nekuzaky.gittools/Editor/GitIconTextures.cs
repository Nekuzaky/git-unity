using System.Collections.Generic;
using UnityEngine;

#if GITTOOLS_VECTOR_GRAPHICS
using System.IO;
using Unity.VectorGraphics;
#endif

namespace GitTools.EditorTools
{
    /// <summary>
    /// Rasterises the Bootstrap Icons artwork into cached textures.
    ///
    /// The SVG work is done by Unity's built-in Vector Graphics module, which only exists
    /// from Unity 6 onwards; the asmdef defines GITTOOLS_VECTOR_GRAPHICS when it is present.
    /// Without it every entry point returns null and the UI falls back to the Unicode
    /// glyphs in <see cref="GitIcons"/>, so the package still works on 2021.3.
    /// </summary>
    public static class GitIconTextures
    {
        /// <summary>True when icons are drawn as artwork rather than as text glyphs.</summary>
        public static bool Available
        {
#if GITTOOLS_VECTOR_GRAPHICS
            get { return true; }
#else
            get { return false; }
#endif
        }

        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Texture for a UI slot at the given pixel size, or null when unavailable. The
        /// artwork is rendered white so callers can tint it with GUI.color.
        /// </summary>
        public static Texture2D Get(string slot, int size)
        {
#if GITTOOLS_VECTOR_GRAPHICS
            if (string.IsNullOrEmpty(slot) || size <= 0) return null;

            string iconName;
            if (!GitIconArtwork.Slots.TryGetValue(slot, out iconName)) return null;

            string artwork;
            if (!GitIconArtwork.Paths.TryGetValue(iconName, out artwork)) return null;

            var key = iconName + "@" + size;

            Texture2D texture;
            // The null test also catches a texture dropped by a domain reload.
            if (s_Cache.TryGetValue(key, out texture) && texture != null) return texture;

            texture = Render(artwork, size);
            s_Cache[key] = texture;
            return texture;
#else
            return null;
#endif
        }

        /// <summary>Drops the cached textures, for instance after a scale change.</summary>
        public static void Clear()
        {
            foreach (var texture in s_Cache.Values)
                if (texture != null) Object.DestroyImmediate(texture);

            s_Cache.Clear();
        }

#if GITTOOLS_VECTOR_GRAPHICS
        static Material s_Material;

        static Material RenderMaterial
        {
            get
            {
                if (s_Material != null) return s_Material;

                // Unlit/VectorGradient ships with the standalone vector graphics package and
                // is absent from the built-in module, so fall back through shaders that the
                // engine always has. Solid-fill artwork only needs vertex colours.
                var shader = Shader.Find("Sprites/Default")
                             ?? Shader.Find("UI/Default")
                             ?? Shader.Find("Hidden/Internal-Colored");

                if (shader == null) return null;

                s_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return s_Material;
            }
        }

        static Texture2D Render(string artwork, int size)
        {
            var material = RenderMaterial;
            if (material == null) return null;

            // Bootstrap Icons are authored on a 16x16 viewBox; white so GUI.color can tint.
            var document =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\" " +
                "viewBox=\"0 0 16 16\" fill=\"#FFFFFF\">" + artwork + "</svg>";

            try
            {
                SVGParser.SceneInfo scene;
                using (var reader = new StringReader(document))
                    scene = SVGParser.ImportSVG(reader, ViewportOptions.PreserveViewport, 0f, 1f, 100, 100);

                var options = new VectorUtils.TessellationOptions
                {
                    StepDistance = 1f,
                    MaxCordDeviation = 0.05f,
                    MaxTanAngleDeviation = 0.05f,
                    SamplingStepSize = 0.01f,
                };

                var geometry = VectorUtils.TessellateScene(scene.Scene, options);
                if (geometry == null || geometry.Count == 0) return null;

                var sprite = VectorUtils.BuildSprite(geometry, 16f, VectorUtils.Alignment.Center, Vector2.zero, 128, true);
                if (sprite == null) return null;

                // 4x supersampling keeps the 16 px artwork clean at editor sizes.
                var texture = VectorUtils.RenderSpriteToTexture2D(sprite, size, size, material, 4, true);

                Object.DestroyImmediate(sprite);

                if (texture != null) texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }
            catch (System.Exception e)
            {
                // A malformed path must never take the window down with it.
                Debug.LogWarning("[Git Tools] Could not rasterise an icon: " + e.Message);
                return null;
            }
        }
#endif
    }
}
