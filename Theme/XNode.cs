using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace HudBreakout
{
    // Forgiving wrapper around an XML element: anything missing falls back to the default,
    // so a half cooked theme.xml still loads
    internal class XNode
    {
        private static readonly Dictionary<string, Vector2> Anchors =
            new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
            {
                { "topleft", new Vector2(0, 1) },
                { "topcenter", new Vector2(0.5f, 1) },
                { "topright", new Vector2(1, 1) },
                { "middleleft", new Vector2(0, 0.5f) },
                { "center", new Vector2(0.5f, 0.5f) },
                { "middlecenter", new Vector2(0.5f, 0.5f) },
                { "middleright", new Vector2(1, 0.5f) },
                { "bottomleft", new Vector2(0, 0) },
                { "bottomcenter", new Vector2(0.5f, 0) },
                { "bottomright", new Vector2(1, 0) }
            };

        private readonly XmlElement _element;

        public XNode(XmlElement element)
        {
            _element = element;
        }

        public bool Exists => _element != null;

        public static XNode Parse(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return new XNode(doc.DocumentElement);
        }

        public string GetString(string name, string defaultValue)
        {
            var value = _element?.GetAttribute(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public float GetFloat(string name, float defaultValue)
        {
            var ok = float.TryParse(GetString(name, null), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var value);
            return ok ? value : defaultValue;
        }

        public int GetInt(string name, int defaultValue) => Mathf.RoundToInt(GetFloat(name, defaultValue));

        public bool GetBoolean(string name, bool defaultValue)
        {
            var text = GetString(name, null);
            if (text == null) return defaultValue;
            return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1";
        }

        public Color GetColor(string name, Color defaultValue)
        {
            var text = GetString(name, null);
            if (text != null && ColorUtility.TryParseHtmlString(text, out var color)) return color;
            return defaultValue;
        }

        public Vector2 GetAnchor(string name, Vector2 defaultValue)
        {
            return Anchors.TryGetValue(GetString(name, ""), out var anchor) ? anchor : defaultValue;
        }

        public XNode GetChild(string name)
        {
            // an empty node keeps chained calls like GetChild("A").GetChild("B") null-safe
            return GetChildren(name).FirstOrDefault() ?? new XNode(null);
        }

        public List<XNode> GetChildren(string name)
        {
            if (_element == null) return new List<XNode>();

            return _element.ChildNodes.OfType<XmlElement>()
                .Where(child => child.Name == name)
                .Select(child => new XNode(child))
                .ToList();
        }
    }
}