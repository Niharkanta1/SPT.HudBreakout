using System.Collections.Generic;
using EFT.Ballistics;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    // Text-only kill feed, everything about it comes from the theme
    internal class KillFeedView
    {
        private const float LineWidth = 700f;
        private const float LinePadding = 8f;  // added to the font size for the line height
        private const float FadeInTime = 0.2f;
        private const float FadeOutTime = 0.6f;
        private const float SlideDistance = 20f;
        private const float SnapAge = 0.02f;   // brand new lines jump straight to their slot
        private const float Smoothing = 12f;

        private class Line
        {
            public RectTransform RectTransform;
            public CanvasGroup Group;
            public float Age, VerticalPosition;
        }

        public RectTransform Root { get; }
        public float ThemeScale { get; }
        public bool ThemeShow { get; }

        private readonly Theme _theme;
        private readonly List<Line> _lines = new List<Line>();
        private readonly int _maxLines, _fontSize;
        private readonly float _lineLifetime, _lineSpacing, _lineHeight, _horizontalAnchor;
        private readonly string _format, _headshotFormat;
        private readonly bool _growUpward;
        private readonly TextAnchor _alignment;
        private readonly Vector2 _position;
        private int _totalKills;

        public KillFeedView(Transform parent, Theme theme)
        {
            _theme = theme;
            var settings = theme.Root.GetChild("KillFeed");
            ThemeShow = settings.GetBoolean("show", true);
            ThemeScale = settings.GetFloat("scale", 1f);

            var anchor = settings.GetAnchor("anchor", new Vector2(0.5f, 0.5f));
            _horizontalAnchor = anchor.x;
            _alignment = UiKit.GetTextAlignment(anchor.x);

            // feeds anchored near the bottom grow upward unless the theme says otherwise
            var grow = settings.GetString("grow", anchor.y < 0.25f ? "Up" : "Down");
            _growUpward = grow.ToLowerInvariant() == "up";

            _position = new Vector2(settings.GetFloat("x", 0), settings.GetFloat("y", -170));
            Root = UiKit.CreateNode(parent, "KillFeed", anchor, anchor, _position, Vector2.zero);

            // 0 or negative would break the trim loop
            _maxLines = Mathf.Max(1, settings.GetInt("max", 4));
            _lineLifetime = settings.GetFloat("life", 5f);
            _fontSize = settings.GetInt("size", 18);
            _lineSpacing = settings.GetFloat("spacing", 4f);
            _lineHeight = _fontSize + LinePadding;

            _format = settings.GetString("format",
                TextLocalization.Get("killfeed.format", "{name}  {c:#8d8d8d}{weapon}  {distance}{distanceUnit}  {part}{/c}"));
            _headshotFormat = settings.GetString("headshotFormat",
                _format + "  {c:#f2cf5b}{t:killfeed.headshot}{/c}");
        }

        public void Reset()
        {
            foreach (var line in _lines) Object.Destroy(line.RectTransform.gameObject);
            _lines.Clear();
            _totalKills = 0;
        }

        public void OnKill(KillEvent killEvent)
        {
            _totalKills++;
            AddLine(killEvent);
        }

        public void Preview()
        {
            PlaceRoot();
            // goes straight to AddLine so the fake kill doesn't count toward {kills}
            AddLine(new KillEvent
            {
                Name = "RAIDER",
                Weapon = "M4A1",
                Part = EBodyPart.Head,
                Distance = 42,
                Level = 35,
                Headshot = true,
                Tag = "PMC BOT"
            });
        }

        public void Update(float deltaTime)
        {
            PlaceRoot();
            RemoveExpired(deltaTime);
            AnimateLines(deltaTime);
        }

        private void AddLine(KillEvent killEvent)
        {
            var format = killEvent.Headshot ? _headshotFormat : _format;
            var label = UiKit.CreateLabel(Root, "Line", Fill(format, killEvent), _fontSize, _theme.Text, _alignment,
                new Vector2(0.5f, 0.5f), new Vector2(_horizontalAnchor, 0.5f),
                Vector2.zero, new Vector2(LineWidth, _lineHeight));

            var group = label.rectTransform.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _lines.Insert(0, new Line { RectTransform = label.rectTransform, Group = group });

            while (_lines.Count > _maxLines) RemoveLine(_lines.Count - 1);
        }

        private string Fill(string format, KillEvent killEvent)
        {
            var text = format
                .Replace("{name}", string.IsNullOrEmpty(killEvent.Name)
                    ? TextLocalization.Get("killfeed.unknown")
                    : killEvent.Name)
                .Replace("{weapon}", string.IsNullOrEmpty(killEvent.Weapon) ? "" : killEvent.Weapon.ToUpperInvariant())
                .Replace("{distance}", Mathf.RoundToInt(killEvent.Distance).ToString())
                .Replace("{distanceUnit}", TextLocalization.Get("killfeed.distance_unit"))
                .Replace("{t:killfeed.headshot}", TextLocalization.Get("killfeed.headshot"))
                .Replace("{part}", VitalsView.Name(killEvent.Part))
                .Replace("{level}", killEvent.Level.ToString())
                .Replace("{tag}", killEvent.Tag ?? "")
                .Replace("{kills}", _totalKills.ToString());
            return Theme.Rich(text);
        }

        private void PlaceRoot()
        {
            var position = _position + new Vector2(Settings.KillFeedX.Value, Settings.KillFeedY.Value);
            if (Root.anchoredPosition != position) Root.anchoredPosition = position;
        }

        // walks backwards so removing a line doesn't shift the ones we haven't checked yet
        private void RemoveExpired(float deltaTime)
        {
            for (var index = _lines.Count - 1; index >= 0; index--)
            {
                _lines[index].Age += deltaTime;
                if (_lines[index].Age > _lineLifetime) RemoveLine(index);
            }
        }

        private void RemoveLine(int index)
        {
            Object.Destroy(_lines[index].RectTransform.gameObject);
            _lines.RemoveAt(index);
        }

        // newest line sits in slot 0, older ones slide away from it, and everything fades in and out
        private void AnimateLines(float deltaTime)
        {
            var step = (_lineHeight + _lineSpacing) * (_growUpward ? 1f : -1f);
            var slideSign = _horizontalAnchor > 0.5f ? 1f : -1f;
            var follow = 1f - Mathf.Exp(-Smoothing * deltaTime);

            for (var index = 0; index < _lines.Count; index++)
            {
                var line = _lines[index];
                var targetY = index * step;
                line.VerticalPosition = line.Age < SnapAge
                    ? targetY
                    : Mathf.Lerp(line.VerticalPosition, targetY, follow);

                var fadeIn = Mathf.Clamp01(line.Age / FadeInTime);
                var slideX = (1f - fadeIn) * SlideDistance * slideSign;
                line.RectTransform.anchoredPosition = new Vector2(slideX, line.VerticalPosition);
                line.Group.alpha = fadeIn * Mathf.Clamp01((_lineLifetime - line.Age) / FadeOutTime);
            }
        }
    }
}