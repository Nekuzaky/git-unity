using System.Collections.Generic;
using UnityEditor;
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
    ///
    /// Rasterising happens off the GUI callback, on a delayCall: drawing a mesh into a
    /// render texture in the middle of IMGUI's own repaint fights with its render state.
    /// A slot that is not ready yet returns null for that frame and the caller falls back
    /// to the glyph, which is why <see cref="GitStyles"/> always passes both.
    /// </summary>
    public static class GitIconTextures
    {
        /// <summary>True when icons can be drawn as artwork rather than as text glyphs.</summary>
        public static bool Available
        {
#if GITTOOLS_VECTOR_GRAPHICS
            get { return true; }
#else
            get { return false; }
#endif
        }

        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();

        // A slot that failed to rasterise is remembered so it is never retried: without this
        // a broken path would be re-rendered on every repaint and flood the console.
        static readonly HashSet<string> s_Failed = new HashSet<string>();
        static readonly HashSet<string> s_Pending = new HashSet<string>();

        /// <summary>
        /// Texture for a UI slot at the given pixel size, or null when it is unavailable or
        /// not rasterised yet. The artwork is white so callers can tint it with GUI.color.
        /// </summary>
        public static Texture2D Get(string slot, int size)
        {
#if GITTOOLS_VECTOR_GRAPHICS
            if (string.IsNullOrEmpty(slot) || size <= 0) return null;

            string iconName;
            if (!GitIconArtwork.Slots.TryGetValue(slot, out iconName)) return null;

            var key = iconName + "@" + size;
            if (s_Failed.Contains(key)) return null;

            Texture2D texture;
            // The null test also catches a texture dropped by a domain reload.
            if (s_Cache.TryGetValue(key, out texture) && texture != null) return texture;

            Queue(key, iconName, size);
            return null;
#else
            return null;
#endif
        }

        /// <summary>Drops the cached textures so they are rasterised again on demand.</summary>
        public static void Clear()
        {
            foreach (var texture in s_Cache.Values)
                if (texture != null) Object.DestroyImmediate(texture);

            s_Cache.Clear();
            s_Failed.Clear();
            s_Pending.Clear();
        }

#if GITTOOLS_VECTOR_GRAPHICS
        static Material s_Material;

        static void Queue(string key, string iconName, int size)
        {
            if (!s_Pending.Add(key)) return;

            EditorApplication.delayCall += () =>
            {
                s_Pending.Remove(key);

                string artwork;
                if (!GitIconArtwork.Paths.TryGetValue(iconName, out artwork))
                {
                    s_Failed.Add(key);
                    return;
                }

                var texture = Render(artwork, size);
                if (texture == null)
                {
                    s_Failed.Add(key);
                    return;
                }

                s_Cache[key] = texture;
                RepaintWindows();
            };
        }

        static void RepaintWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<GitDashboardWindow>())
                window.Repaint();
        }

        static Material RenderMaterial
        {
            get
            {
                if (s_Material != null) return s_Material;

                // Unlit/VectorGradient ships with the standalone vector graphics package and
                // is absent from the built-in module, so fall back through shaders the engine
                // always has. Solid-fill artwork only needs vertex colours.
                var shader = Shader.Find("Sprites/Default")
                             ?? Shader.Find("UI/Default")
                             ?? Shader.Find("Hidden/Internal-Colored");

                if (shader == null) return null;

                s_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                s_Material.mainTexture = Texture2D.whiteTexture;
                return s_Material;
            }
        }

        /// <summary>
        /// Tessellates the artwork and draws it straight into a render texture.
        ///
        /// This deliberately avoids VectorUtils.BuildSprite: it builds a Sprite and calls
        /// Sprite.OverrideGeometry, which Unity 6 refuses ("Not allowed to override geometry
        /// on sprite"), leaving a null sprite and an error per icon per frame. FillMesh gives
        /// the same geometry as a Mesh with no Sprite in the way.
        /// </summary>
        static Texture2D Render(string artwork, int size)
        {
            var material = RenderMaterial;
            if (material == null) return null;

            // Bootstrap Icons are authored on a 16x16 viewBox; white so GUI.color can tint.
            var document =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\" " +
                "viewBox=\"0 0 16 16\" fill=\"#FFFFFF\">" + artwork + "</svg>";

            Mesh mesh = null;
            RenderTexture target = null;
            var previousTarget = RenderTexture.active;

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

                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                VectorUtils.FillMesh(mesh, geometry, 1f, false);

                var bounds = mesh.bounds;
                if (bounds.size.x <= 0f || bounds.size.y <= 0f) return null;

                // Square the framing so a wide or tall icon keeps its proportions.
                var extent = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f;
                var centre = bounds.center;

                target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                RenderTexture.active = target;
                GL.Clear(true, true, new Color(1f, 1f, 1f, 0f));

                GL.PushMatrix();
                GL.LoadIdentity();      // drop whatever modelview the caller left behind
                GL.LoadProjectionMatrix(Matrix4x4.Ortho(
                    centre.x - extent, centre.x + extent,
                    centre.y + extent, centre.y - extent,   // flipped: SVG y grows downwards
                    -1f, 1f));

                material.SetPass(0);
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);

                GL.PopMatrix();

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                };

                texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                texture.Apply();

                return texture;
            }
            catch (System.Exception e)
            {
                // A malformed path must never take the window down with it.
                Debug.LogWarning("[Git Tools] Could not rasterise an icon: " + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previousTarget;

                if (target != null) Object.DestroyImmediate(target);
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }
#endif
    }
}
