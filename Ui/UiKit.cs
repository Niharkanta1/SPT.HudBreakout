using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    // Small helpers for building HUD elements in code. Everything here is created at runtime, so there are no prefabs to maintain.
    internal static class UiKit
    {
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.9f);
        private static readonly Vector2 ShadowOffset = new Vector2(1f, -1f);

        public static Theme CurrentTheme;

        private static Sprite _whiteSprite;
        private static Texture2D _whiteTexture;

        // A plain white sprite you can tint through Image.color. Created lazily on first use.
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null) CreateWhiteSprite();
                return _whiteSprite;
            }
        }

        // Call this when the HUD shuts down. The sprite and texture use HideAndDontSave, so Unity won't clean them up on its own.
        public static void ReleaseOwnedResources()
        {
            if (_whiteSprite != null) Object.Destroy(_whiteSprite);
            if (_whiteTexture != null) Object.Destroy(_whiteTexture);

            _whiteSprite = null;
            _whiteTexture = null;
            CurrentTheme = null;
        }

        public static RectTransform CreateNode(
            Transform parent,
            string objectName,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            // Min and max are the same point, so the node keeps a fixed size
            // instead of stretching with its parent.
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        public static Image CreateImage(
            Transform parent,
            string objectName,
            Sprite sprite,
            Color color,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var rect = CreateNode(parent, objectName, anchor, pivot, anchoredPosition, sizeDelta);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // HUD graphics shouldn't swallow clicks
            return image;
        }

        public static void StretchToFillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Kept so existing callers still compile.
        public static TMP_Text CreateLabel(
            Transform parent,
            string objectName,
            string text,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            return CreateLabel(
                parent, objectName, text, fontSize, color,
                ToTextAlignment(alignment),
                anchor, pivot, anchoredPosition, sizeDelta);
        }

        public static TMP_Text CreateLabel(
            Transform parent,
            string objectName,
            string text,
            int fontSize,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var rect = CreateNode(parent, objectName, anchor, pivot, anchoredPosition, sizeDelta);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (FontHelper.BenderNormal != null) label.font = FontHelper.BenderNormal;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.text = text;
            label.richText = true;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;

            // No theme loaded yet? Default to having the shadow.
            if (CurrentTheme == null || CurrentTheme.Shadow)
            {
                var shadow = rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = ShadowColor;
                shadow.effectDistance = ShadowOffset;
            }

            return label;
        }

        // Picks left / center / right alignment based on where the label is anchored.
        public static TextAnchor GetTextAlignment(float horizontalAnchor)
        {
            if (horizontalAnchor < 0.25f) return TextAnchor.MiddleLeft;
            if (horizontalAnchor > 0.75f) return TextAnchor.MiddleRight;
            return TextAnchor.MiddleCenter;
        }

        private static TextAlignmentOptions ToTextAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static void CreateWhiteSprite()
        {
            // One solid-color pixel is all a tintable sprite needs.
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();

            _whiteSprite = Sprite.Create(
                _whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}