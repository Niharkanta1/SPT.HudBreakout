using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    // Scaling a view's Root pivots on the Root point, not the content, so the gap to the screen border changed
    // with the scale. This scales a node under Root around the content edge facing that border instead.
    internal sealed class ScalePivot
    {
        private const int SeparatePasses = 3;
        private const float PanelGap = 8f;  // HUD pixels kept between panels
        private const float MeasureInterval = 0.1f;

        // horizontal bits of TextAlignmentOptions; the named members also carry a vertical bit
        private const int AlignCenter = 2;
        private const int AlignRight = 4;

        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>(64);

        private readonly RectTransform _root;
        private readonly RectTransform _node;
        private readonly float _themeScale;

        private Vector2 _edge;
        private Vector2 _nudge;
        private float _scale = 1f;
        private float _nextMeasure;
        private Rect _visible;
        private bool _hasVisible;

        private Vector2 PivotOffset => _edge * (1f - _scale) + _nudge;
        private Rect ScreenRect => ((RectTransform)_root.parent).rect;

        public ScalePivot(RectTransform root, float themeScale)
        {
            _root = root;
            _themeScale = themeScale;
            _node = UiKit.CreateNode(root, "Scale", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            ReparentChildren(root, _node);
        }

        public void Update(float hudScale)
        {
            var scale = hudScale * _themeScale;
            var changed = !Mathf.Approximately(scale, _scale);
            _scale = scale;

            var scaleVector = Vector3.one * scale;
            if (_node.localScale != scaleVector) _node.localScale = scaleVector;

            if (changed) Measure();
            _nudge = Vector2.zero;
            ApplyPosition();
        }

        // Scaled panels grow into their neighbours. Moves one panel of each new overlap just far enough to clear,
        // preferring the one that moves away from its own border. Overlaps present at scale 1 are left alone.
        // Does nothing, and measures nothing, while every panel is at scale 1.
        public static void Separate(ScalePivot[] pivots)
        {
            if (!IsAnyScaled(pivots)) return;

            RefreshMeasurements(pivots);

            for (var pass = 0; pass < SeparatePasses; pass++)
            {
                if (!SeparatePass(pivots)) return;
            }
        }

        private static void ReparentChildren(RectTransform root, RectTransform node)
        {
            var children = new List<Transform>(root.childCount);
            foreach (Transform child in root)
                if (child != node) children.Add(child);
            foreach (var child in children)
                child.SetParent(node, false);
        }

        private static bool IsAnyScaled(ScalePivot[] pivots)
        {
            foreach (var pivot in pivots)
                if (!Mathf.Approximately(pivot._scale, 1f)) return true;
            return false;
        }

        // effect rows, toggles and slider edits change the content without changing the scale
        private static void RefreshMeasurements(ScalePivot[] pivots)
        {
            foreach (var pivot in pivots)
            {
                if (Time.unscaledTime < pivot._nextMeasure) continue;
                pivot.Measure();
                pivot.ApplyPosition();
            }
        }

        private static bool SeparatePass(ScalePivot[] pivots)
        {
            var moved = false;
            for (var i = 0; i < pivots.Length; i++)
            {
                for (var j = i + 1; j < pivots.Length; j++)
                    moved |= Separate(pivots[i], pivots[j]);
            }
            return moved;
        }

        private static bool Separate(ScalePivot a, ScalePivot b)
        {
            if (!a.TryGetRects(out var scaledA, out var nominalA) || !b.TryGetRects(out var scaledB, out var nominalB))
                return false;

            if (TryGetOverlap(nominalA, nominalB, out _)) return false;
            if (!TryGetOverlap(scaledA, scaledB, out var overlap)) return false;

            var axis = overlap.x <= overlap.y ? 0 : 1;
            var directionA = scaledA.center[axis] >= scaledB.center[axis] ? 1f : -1f;
            var directionB = -directionA;

            var aAway = a.IsAwayFromBorder(axis, directionA);
            var bAway = b.IsAwayFromBorder(axis, directionB);
            var moveA = aAway != bAway ? aAway : a.BorderGap(scaledA, axis) >= b.BorderGap(scaledB, axis);

            if (moveA) a.Nudge(axis, directionA * overlap[axis]);
            else b.Nudge(axis, directionB * overlap[axis]);
            return true;
        }

        private static bool TryGetOverlap(Rect a, Rect b, out Vector2 overlap)
        {
            overlap = new Vector2(
                Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin),
                Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
            return overlap.x > 0f && overlap.y > 0f;
        }

        private void Measure()
        {
            _nextMeasure = Time.unscaledTime + MeasureInterval;
            if (!TryMeasure(out var layout, out _visible, out _hasVisible)) return;

            // the content edge on the anchored side
            _edge = new Vector2(
                Mathf.Lerp(layout.xMin, layout.xMax, _root.anchorMin.x),
                Mathf.Lerp(layout.yMin, layout.yMax, _root.anchorMin.y));
        }

        private void Nudge(int axis, float amount)
        {
            _nudge[axis] += amount;
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            var position = PivotOffset;
            if (_node.anchoredPosition != position) _node.anchoredPosition = position;
        }

        private bool IsAwayFromBorder(int axis, float direction)
        {
            var anchor = _root.anchorMin[axis];
            if (anchor < 0.5f) return direction > 0f;
            if (anchor > 0.5f) return direction < 0f;
            return true;
        }

        private float BorderGap(Rect rect, int axis)
        {
            var screen = ScreenRect;
            var anchor = _root.anchorMin[axis];
            if (anchor < 0.5f) return rect.min[axis] - screen.min[axis];
            if (anchor > 0.5f) return screen.max[axis] - rect.max[axis];
            return 0f;
        }

        // visible content in the parent's space, now and at scale 1
        private bool TryGetRects(out Rect scaled, out Rect nominal)
        {
            scaled = nominal = default;
            if (!_hasVisible) return false;

            var screen = ScreenRect;
            var origin = screen.min + Vector2.Scale(_root.anchorMin, screen.size) + _root.anchoredPosition;

            scaled = PaddedRect(origin + PivotOffset, _visible, _scale);
            nominal = PaddedRect(origin, _visible, 1f);
            return true;
        }

        // padded so panels closer than the gap count as overlapping
        private static Rect PaddedRect(Vector2 origin, Rect local, float scale)
        {
            var pad = PanelGap * 0.5f;
            return Rect.MinMaxRect(
                origin.x + local.xMin * scale - pad, origin.y + local.yMin * scale - pad,
                origin.x + local.xMax * scale + pad, origin.y + local.yMax * scale + pad);
        }

        // layout: every graphic with a short run of text, so toggles and digit changes don't move the edge
        // visible: only active graphics at their real text width, so panels clear what is actually showing
        private bool TryMeasure(out Rect layout, out Rect visible, out bool hasVisible)
        {
            var layoutBounds = Bounds.Empty;
            var visibleBounds = Bounds.Empty;

            _node.GetComponentsInChildren(true, GraphicBuffer);
            foreach (var graphic in GraphicBuffer)
            {
                var rect = graphic.rectTransform.rect;
                var layoutRect = rect;
                var visibleRect = rect;
                if (graphic is TMP_Text text)
                {
                    layoutRect = TextRun(rect, text, text.fontSize * 4f);
                    visibleRect = TextRun(rect, text, text.preferredWidth);
                }

                Encapsulate(graphic, layoutRect, ref layoutBounds);
                if (graphic.gameObject.activeInHierarchy)
                    Encapsulate(graphic, visibleRect, ref visibleBounds);
            }
            GraphicBuffer.Clear();

            hasVisible = visibleBounds.TryGetRect(out visible);
            return layoutBounds.TryGetRect(out layout);
        }

        // the part of a label's box its text fills, on its aligned side
        private static Rect TextRun(Rect rect, TMP_Text text, float run)
        {
            var width = Mathf.Min(rect.width, run);
            var alignment = (int)text.alignment;
            var left = (alignment & AlignRight) != 0 ? rect.xMax - width
                : (alignment & AlignCenter) != 0 ? rect.center.x - width * 0.5f
                : rect.xMin;
            return new Rect(left, rect.yMin, width, rect.height);
        }

        private void Encapsulate(Graphic graphic, Rect rect, ref Bounds bounds)
        {
            var cornerA = (Vector2)_node.InverseTransformPoint(graphic.rectTransform.TransformPoint(rect.min));
            var cornerB = (Vector2)_node.InverseTransformPoint(graphic.rectTransform.TransformPoint(rect.max));
            bounds.Add(cornerA, cornerB);
        }

        private struct Bounds
        {
            private Vector2 _min, _max;

            public static Bounds Empty => new Bounds
            {
                _min = new Vector2(float.MaxValue, float.MaxValue),
                _max = new Vector2(float.MinValue, float.MinValue)
            };

            public void Add(Vector2 a, Vector2 b)
            {
                _min = Vector2.Min(_min, Vector2.Min(a, b));
                _max = Vector2.Max(_max, Vector2.Max(a, b));
            }

            public bool TryGetRect(out Rect rect)
            {
                var found = _max.x >= _min.x && _max.y >= _min.y;
                rect = found ? Rect.MinMaxRect(_min.x, _min.y, _max.x, _max.y) : default;
                return found;
            }
        }
    }
}
