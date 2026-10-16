using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace HudBreakout
{
    internal static class TextLocalization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Catalogs =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> _current = new Dictionary<string, string>();
        private static Dictionary<string, string> _english = new Dictionary<string, string>();
        private static string _directory;

        public static string CurrentLanguage { get; private set; } = "en";
        public static string[] AvailableLanguages
        {
            get
            {
                var locales = Catalogs
                    .Where(catalog => catalog.Value.Count > 0)
                    .Select(catalog => catalog.Key)
                    .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!locales.Exists(language => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)))
                    locales.Insert(0, "en");
                else
                {
                    var englishIndex = locales.FindIndex(language =>
                        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase));
                    var english = locales[englishIndex];
                    locales.RemoveAt(englishIndex);
                    locales.Insert(0, english);
                }
                locales.Insert(0, "Auto");
                var languages = locales;
                return languages.ToArray();
            }
        }

        public static void Initialize(string directory, string language)
        {
            _directory = directory;
            Catalogs.Clear();
            MissingKeys.Clear();

            if (Directory.Exists(_directory))
            {
                foreach (var path in Directory.GetFiles(_directory, "*.xml")
                    .OrderBy(file => Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    var locale = Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrWhiteSpace(locale)) continue;
                    Catalogs[locale] = LoadCatalog(path);
                }
            }

            if (!Catalogs.TryGetValue("en", out _english))
            {
                HudBreakoutPlugin.Log.LogError("Required English localization catalog not found in '" + _directory + "'.");
                _english = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            SetLanguage(language);
        }

        public static void SetLanguage(string language)
        {
            var resolved = ResolveLanguage(language);
            CurrentLanguage = resolved;

            if (Catalogs.TryGetValue(resolved, out var catalog) && catalog.Count > 0)
            {
                _current = catalog;
                return;
            }

            _current = _english;
            if (resolved != "en")
                HudBreakoutPlugin.Log.LogWarning("Localization catalog '" + resolved + "' unavailable; using English.");
        }

        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";

            if (_current.TryGetValue(key, out var localized)) return localized;
            if (_english.TryGetValue(key, out var english)) return english;
            if (fallback != null) return fallback;

            if (MissingKeys.Add(key))
                HudBreakoutPlugin.Log.LogWarning("Localization key '" + key + "' is missing; displaying the key.");
            return key;
        }

        public static string ResolveThemeText(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '@') return value ?? "";
            var key = value.Substring(1);
            return Get(key, key);
        }

        private static string ResolveLanguage(string language)
        {
            var requested = (language ?? "").Trim();
            switch (requested.ToLowerInvariant())
            {
                case "ru":
                case "russian":
                case "русский":
                    return FindCatalog("ru") ?? "en";
                case "zh-cn":
                case "chinese (simplified)":
                case "简体中文":
                    return FindCatalog("zh-CN") ?? "en";
                case "en":
                case "english":
                    return FindCatalog("en") ?? "en";
                case "auto":
                    return AutoLanguage();
            }

            return FindCatalog(requested) ?? "en";
        }

        private static string AutoLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            var match = FindCatalog(culture.Name);
            if (match != null) return match;

            match = FindCatalog(culture.TwoLetterISOLanguageName);
            if (match != null) return match;
            if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return FindCatalog("zh-CN") ?? FindCatalog("zh") ?? "en";
            return FindCatalog("en") ?? "en";
        }

        private static string FindCatalog(string language)
        {
            foreach (var locale in Catalogs.Keys)
                if (locale.Equals(language, StringComparison.OrdinalIgnoreCase))
                    return locale;
            return null;
        }

        private static Dictionary<string, string> LoadCatalog(string path)
        {
            var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var root = XDocument.Load(path).Root;
                if (root == null || root.Name != "Localization")
                    throw new InvalidDataException("Expected a Localization root element.");

                foreach (var entry in root.Elements("Text"))
                {
                    var key = (string)entry.Attribute("key");
                    if (string.IsNullOrWhiteSpace(key))
                        throw new InvalidDataException("Every Text entry must have a non-empty key.");
                    if (catalog.ContainsKey(key))
                        throw new InvalidDataException("Duplicate localization key '" + key + "'.");
                    catalog.Add(key, entry.Value);
                }
            }
            catch (IOException ex)
            {
                HudBreakoutPlugin.Log.LogError("Could not read localization catalog '" + path + "': " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                HudBreakoutPlugin.Log.LogError("Could not access localization catalog '" + path + "': " + ex.Message);
            }
            catch (XmlException ex)
            {
                HudBreakoutPlugin.Log.LogError("Invalid localization XML '" + path + "': " + ex.Message);
            }
            catch (InvalidDataException ex)
            {
                HudBreakoutPlugin.Log.LogError("Invalid localization catalog '" + path + "': " + ex.Message);
            }

            return catalog;
        }
    }
}
