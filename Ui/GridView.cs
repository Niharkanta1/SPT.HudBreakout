using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal sealed class GridView
    {
        private const float Spacing = 50f;
        private const float MajorSpacing = 250f;
        private const float MinorThickness = 1f;
        private const float CenterThickness = 2f;

        private static readonly Color MinorColor = new Color(1f, 1f, 1f, 0.2f);
        private static readonly Color MajorColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color CenterColor = new Color(1f, 1f, 1f, 0.55f);

        private readonly RectTransform _root;
        private readonly Canvas _canvas;
        private readonly List<Image> _lines = new List<Image>();
        private Vector2 _builtSize = new Vector2(-1f, -1f);

        public GridView(Transform parent)
        {
            _canvas = parent.GetComponentInParent<Canvas>();
            _root = UiKit.CreateNode(parent, "Grid", Vector2.zero, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            UiKit.StretchToFillParent(_root);
            _root.SetAsFirstSibling();
        }

        public void Update(bool enabled)
        {
            if (_root.gameObject.activeSelf != enabled)
                _root.gameObject.SetActive(enabled);

            var size = _root.rect.size;
            if (Mathf.Approximately(size.x, _builtSize.x) && Mathf.Approximately(size.y, _builtSize.y))
                return;

            RebuildLines(size);
        }

        private void RebuildLines(Vector2 size)
        {
            foreach (var line in _lines)
                Object.Destroy(line.gameObject);
            _lines.Clear();
            _builtSize = size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            AddVerticalLines(size.x);
            AddHorizontalLines(size.y);
        }

        private void AddVerticalLines(float width)
        {
            var halfWidth = width * 0.5f;
            AddVerticalLine(0f);
            for (var x = Spacing; x <= halfWidth; x += Spacing)
            {
                AddVerticalLine(x);
                AddVerticalLine(-x);
            }
        }

        private void AddHorizontalLines(float height)
        {
            var halfHeight = height * 0.5f;
            AddHorizontalLine(0f);
            for (var y = Spacing; y <= halfHeight; y += Spacing)
            {
                AddHorizontalLine(y);
                AddHorizontalLine(-y);
            }
        }

        private void AddVerticalLine(float x)
        {
            var isCenter = Mathf.Approximately(x, 0f);
            var isMajor = Mathf.Approximately(Mathf.Repeat(Mathf.Abs(x), MajorSpacing), 0f);
            AddLine(new Vector2(x, 0f), new Vector2(ToCanvasUnits(isCenter ? CenterThickness : MinorThickness), _root.rect.height),
                isCenter ? CenterColor : isMajor ? MajorColor : MinorColor);
        }

        private void AddHorizontalLine(float y)
        {
            var isCenter = Mathf.Approximately(y, 0f);
            var isMajor = Mathf.Approximately(Mathf.Repeat(Mathf.Abs(y), MajorSpacing), 0f);
            AddLine(new Vector2(0f, y), new Vector2(_root.rect.width, ToCanvasUnits(isCenter ? CenterThickness : MinorThickness)),
                isCenter ? CenterColor : isMajor ? MajorColor : MinorColor);
        }

        private float ToCanvasUnits(float pixels)
        {
            var scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            return pixels / Mathf.Max(scaleFactor, 0.01f);
        }

        private void AddLine(Vector2 position, Vector2 size, Color color)
        {
            var image = UiKit.CreateImage(_root, "GridLine", UiKit.WhiteSprite, color,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            _lines.Add(image);
        }
    }
}
