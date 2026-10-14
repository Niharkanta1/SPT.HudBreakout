using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

namespace HudBreakout
{
    internal class Theme
    {
        private static readonly Regex ColorTag = new Regex(@"\{c:([^}]+)\}");
        private static readonly Regex SizeTag = new Regex(@"\{size:(\d+)\}");

        public XNode Root;
        public string DirectoryPath, Name;
        public Color Good, Warn, Bad, Text, Dim, Gold, Lost, Default;
        public bool Shadow;
        public bool IconsEnabled;

        private XNode _iconColors;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        public static Theme Load(string themesDir, string name)
        {
            var theme = new Theme { Name = name, DirectoryPath = Path.Combine(themesDir, name) };
            Directory.CreateDirectory(theme.DirectoryPath);

            var file = FindThemeFile(theme.DirectoryPath);
            theme.Root = new XNode(null);
            try
            {
                if (File.Exists(file))
                {
                    var doc = new XmlDocument();
                    doc.Load(file);
                    theme.Root = new XNode(doc.DocumentElement);
                }
                else
                {
                    HudBreakoutPlugin.Log.LogWarning("no theme file, using defaults: " + file);
                }
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("bad theme xml " + file + ": " + ex.Message);
            }

            var colors = theme.Root.GetChild("Colors");
            theme.Good = colors.GetColor("good", Hex("#8fd16f"));
            theme.Warn = colors.GetColor("warn", Hex("#e6c04a"));
            theme.Bad = colors.GetColor("bad", Hex("#d9473d"));
            theme.Text = colors.GetColor("text", Hex("#e2e2e2"));
            theme.Dim = colors.GetColor("dim", Hex("#8d8d8d"));
            theme.Gold = colors.GetColor("highlight", Hex("#f2cf5b"));
            theme.Lost = colors.GetColor("lost", Hex("#3a2a2a"));
            theme.Default = colors.GetColor("default", Hex("#FFFFFF"));

            // shadow can sit on the root or under <Font>
            theme.Shadow = theme.Root.GetBoolean("shadow", theme.Root.GetChild("Font").GetBoolean("shadow", true));
            theme.IconsEnabled = theme.Root.GetChild("Icons").GetBoolean("enabled", true);
            theme._iconColors = theme.Root.GetChild("IconColors");
            return theme;
        }

        // match theme.xml case-insensitively
        private static string FindThemeFile(string dir)
        {
            return Directory.GetFiles(dir).FirstOrDefault(file =>
                       string.Equals(Path.GetFileName(file), "theme.xml", StringComparison.OrdinalIgnoreCase))
                   ?? Path.Combine(dir, "theme.xml");
        }

        public Color IconColor(string name, Color fallback) => _iconColors.GetColor(name, fallback);

        private static Color Hex(string text)
        {
            ColorUtility.TryParseHtmlString(text, out var color);
            return color;
        }

        public Color HealthColor(float ratio)
        {
            return ratio > 0.5f
                ? Color.Lerp(Warn, Good, (ratio - 0.5f) * 2f)
                : Color.Lerp(Bad, Warn, ratio * 2f);
        }

        public Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return LoadCached(path, Path.Combine(DirectoryPath, path));
        }

        public Sprite LoadThemeSprite(string path)
        {
            var sprite = LoadSprite(path);
            if (sprite != null || string.IsNullOrEmpty(path)) return sprite;

            var minimal = Path.Combine(Path.GetDirectoryName(DirectoryPath), "Minimal");
            return LoadCached("default-theme-sprite:" + path, Path.Combine(minimal, path));
        }

        // theme icon, then the same icon from Minimal, then Minimal's default.png
        public Sprite LoadIconSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var sprite = LoadSprite(path);
            if (sprite != null) return sprite;

            var minimal = Path.Combine(Path.GetDirectoryName(DirectoryPath), "Minimal");
            sprite = LoadCached("default-theme:" + path, Path.Combine(minimal, path));
            if (sprite != null) return sprite;

            return LoadCached("default-icon", Path.Combine(minimal, "Icons", "default.png"));
        }

        public Sprite LoadPartMask(string file, XNode settings)
        {
            if (string.IsNullOrEmpty(file)) return null;

            var key = "mask:" + file;
            if (_sprites.TryGetValue(key, out var cached)) return cached;

            var tex = LoadTexture(Path.Combine(DirectoryPath, file));
            if (tex != null && settings.GetString("maskMode", "alpha").Equals("luminance", StringComparison.OrdinalIgnoreCase))
                ApplyLuminanceMask(tex, settings);

            var sprite = tex != null ? MakeSprite(tex) : null;
            _sprites[key] = sprite;
            return sprite;
        }

        // Turns a dark-on-light mask into white with the shape in alpha
        private static void ApplyLuminanceMask(Texture2D tex, XNode settings)
        {
            var threshold = Mathf.Clamp01(settings.GetFloat("maskThreshold", 0.84f));
            var softness = Mathf.Max(0.001f, settings.GetFloat("maskSoftness", 0.12f));
            var pixels = tex.GetPixels32();

            int dark = 0, visible = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.a == 0) continue;
                visible++;
                if (Luma(pixel) < threshold) dark++;
            }

            // white-on-transparent masks already carry the shape in alpha, so skip luminance for those
            var useLuma = visible > 0 && dark > visible * 0.005f;
            for (var index = 0; index < pixels.Length; index++)
            {
                var alpha = pixels[index].a / 255f;
                if (useLuma) alpha *= Mathf.Clamp01((threshold - Luma(pixels[index])) / softness);
                pixels[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        private static float Luma(Color32 color) => (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) / 255f;

        private static Sprite MakeSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        // missing files are cached as null too, so we don't hit the disk every call
        private Sprite LoadCached(string key, string path)
        {
            if (_sprites.TryGetValue(key, out var cached)) return cached;

            var tex = LoadTexture(path);
            var sprite = tex != null ? MakeSprite(tex) : null;
            _sprites[key] = sprite;
            return sprite;
        }

        private Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                if (tex.LoadImage(File.ReadAllBytes(path)))
                {
                    _textures.Add(tex);
                    return tex;
                }
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("couldn't read image " + path + ": " + ex.Message);
            }

            UnityEngine.Object.Destroy(tex);
            HudBreakoutPlugin.Log.LogWarning("bad image: " + path);
            return null;
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values)
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in _textures)
                if (texture != null) UnityEngine.Object.Destroy(texture);

            _sprites.Clear();
            _textures.Clear();
        }

        // {c:#hex}..{/c}, {b}, {i}, {size:N} to Unity rich text
        public static string Rich(string text)
        {
            text = ColorTag.Replace(text, "<color=$1>");
            text = SizeTag.Replace(text, "<size=$1>");
            return text.Replace("{/c}", "</color>").Replace("{/size}", "</size>")
                .Replace("{b}", "<b>").Replace("{/b}", "</b>")
                .Replace("{i}", "<i>").Replace("{/i}", "</i>");
        }
    }
}