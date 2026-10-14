using EFT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal class CombatView
    {
        private const float Expired = 99f; // any age past every lifetime
        private const int NumberPoolSize = 14;
        private const int IndicatorPoolSize = 6;

        private class Number
        {
            public TMP_Text Text;
            public float Age = Expired, Life, Drift;
            public Vector2 Start;
        }

        private class Indicator
        {
            public RectTransform Pivot;
            public Image Icon;
            public float Age = Expired, Strength;
            public Vector3 Direction;
        }

        private struct HitColors
        {
            public Color Normal, Headshot, Kill;

            public Color Pick(bool killed, bool headshot) => killed ? Kill : headshot ? Headshot : Normal;
        }

        public RectTransform Root { get; }
        public float ThemeScale => 1f;

        private readonly Theme _theme;

        // hit marker
        private readonly RectTransform _markerRoot;
        private readonly Image _markerIcon;
        private readonly bool _markerShow;
        private readonly float _markerDuration, _markerPunch, _markerThickness, _markerLength, _markerGap;
        private readonly HitColors _markerColors;
        private float _markerAge = Expired, _markerLife, _markerPunchScale = 1f;
        private Color _markerColor = Color.white;

        // damage numbers
        private readonly Number[] _numbers = new Number[NumberPoolSize];
        private readonly bool _numbersShow;
        private readonly float _numberLife, _numberRise, _numberSpawnRise;
        private readonly HitColors _numberColors;
        private int _nextNumber, _liveNumbers;

        // damage direction
        private readonly Indicator[] _indicators;
        private readonly bool _directionShow;
        private readonly float _directionLife;
        private readonly Color _directionColor;
        private int _nextIndicator, _liveIndicators;

        public CombatView(Transform parent, Theme theme)
        {
            _theme = theme;
            var hitMarker = theme.Root.GetChild("HitMarker");
            var damageNumbers = theme.Root.GetChild("DamageNumbers");
            var damageDirection = theme.Root.GetChild("DamageDirection");

            // theme values only change on rebuild, so read them once instead of parsing XML on every hit
            _markerShow = hitMarker.GetBoolean("show", true);
            _markerDuration = PositiveOrDefault(hitMarker.GetFloat("duration", 0.25f), 0.25f);
            _markerPunch = hitMarker.GetFloat("punch", 1.2f);
            _markerThickness = hitMarker.GetFloat("thickness", 1.5f);
            _markerLength = hitMarker.GetFloat("length", 7f);
            _markerGap = hitMarker.GetFloat("gap", 5f);
            _markerColors = ReadColors(hitMarker, theme);

            _numbersShow = damageNumbers.GetBoolean("show", true);
            _numberLife = PositiveOrDefault(damageNumbers.GetFloat("life", 0.9f), 0.9f);
            _numberRise = damageNumbers.GetFloat("rise", 70f);
            _numberSpawnRise = damageNumbers.GetFloat("spawnRise", 50f); // px above the hit point
            _numberColors = ReadColors(damageNumbers, theme);

            _directionShow = damageDirection.GetBoolean("show", true);
            _directionLife = PositiveOrDefault(damageDirection.GetFloat("life", 1.5f), 1.5f);
            _directionColor = damageDirection.GetColor("color", theme.Bad);

            var center = new Vector2(0.5f, 0.5f);
            Root = UiKit.CreateNode(parent, "Combat", center, center, Vector2.zero, Vector2.zero);

            _markerRoot = UiKit.CreateNode(Root, "Marker", center, center, Vector2.zero, Vector2.zero);
            var isCross = hitMarker.GetString("style", "X").Equals("cross", System.StringComparison.OrdinalIgnoreCase);
            var markerFile = isCross
                ? hitMarker.GetString("crossImage", "Icons/hit_marker_cross.png")
                : hitMarker.GetString("image", "Icons/hit_marker_x.png");
            _markerIcon = UiKit.CreateImage(_markerRoot, "Icon", theme.LoadIconSprite(markerFile), Color.white,
                center, center, Vector2.zero, HitMarkerSize());
            _markerRoot.gameObject.SetActive(false);

            var numberFontSize = damageNumbers.GetInt("size", 24);
            for (var index = 0; index < _numbers.Length; index++)
            {
                var label = UiKit.CreateLabel(Root, "Dmg" + index, "", numberFontSize, theme.Text,
                    TextAnchor.MiddleCenter, center, center, Vector2.zero, new Vector2(120, 40));
                label.gameObject.SetActive(false);
                _numbers[index] = new Number { Text = label };
            }

            // no icons in the theme means no direction indicators at all
            _indicators = theme.IconsEnabled ? new Indicator[IndicatorPoolSize] : new Indicator[0];

            var radius = damageDirection.GetFloat("radius", 190f);
            var iconLength = damageDirection.GetFloat("length", 80f);
            var iconSprite = theme.LoadIconSprite(damageDirection.GetString("image", "Icons/damage_direction.png"));
            var iconSize = IndicatorSize(damageDirection, iconSprite, iconLength);

            for (var index = 0; index < _indicators.Length; index++)
            {
                var pivot = UiKit.CreateNode(Root, "Dir" + index, center, center, Vector2.zero, Vector2.zero);
                var icon = UiKit.CreateImage(pivot, "Icon", iconSprite, theme.Bad, center, center,
                    new Vector2(0, radius), iconSize);

                // safety net if a custom width doesn't match the sprite
                icon.preserveAspect = true;
                pivot.gameObject.SetActive(false);
                _indicators[index] = new Indicator { Pivot = pivot, Icon = icon };
            }
        }

        private bool CanShowMarker => Settings.ShowHitMarker.Value && _markerShow;
        private bool CanShowNumbers => Settings.ShowDamageNumbers.Value && _numbersShow;
        private bool CanShowDirection =>
            _indicators.Length > 0 && Settings.ShowDamageDirection.Value && _directionShow;

        private static Vector2 MarkerOffset => new Vector2(Settings.HitMarkerX.Value, Settings.HitMarkerY.Value);
        private static Vector2 NumberOffset => new Vector2(Settings.DamageNumbersX.Value, Settings.DamageNumbersY.Value);
        private static Vector2 DirectionOffset =>
            new Vector2(Settings.DamageDirectionX.Value, Settings.DamageDirectionY.Value);

        public void Reset()
        {
            _markerAge = Expired;
            _markerLife = 0f;
            _markerRoot.gameObject.SetActive(false);

            foreach (var number in _numbers)
            {
                number.Age = Expired;
                number.Text.gameObject.SetActive(false);
            }

            foreach (var indicator in _indicators)
            {
                indicator.Age = Expired;
                indicator.Pivot.gameObject.SetActive(false);
            }

            _liveNumbers = 0;
            _liveIndicators = 0;
        }

        public void OnHit(HitEvent hitEvent)
        {
            var headshot = hitEvent.Part == EBodyPart.Head;

            if (CanShowMarker)
            {
                _markerIcon.rectTransform.sizeDelta = HitMarkerSize();
                _markerRoot.anchoredPosition = MarkerOffset;
                _markerAge = 0f;
                _markerLife = _markerDuration * (hitEvent.Killed ? 2f : 1f);
                _markerPunchScale = _markerPunch * (hitEvent.Killed ? 1.3f : 1f);
                _markerColor = _markerColors.Pick(hitEvent.Killed, headshot);
            }

            if (!CanShowNumbers || hitEvent.Damage < 0.5f) return;

            // number spawns at the impact point, or the screen center if it's behind the camera
            var position = Vector2.zero;
            var cam = Camera.main;
            if (cam != null)
            {
                var screen = cam.WorldToScreenPoint(hitEvent.Point);
                if (screen.z > 0) position = HudRoot.ScreenToLocal(screen);
            }

            var number = NextNumber();
            number.Age = 0f;
            number.Life = _numberLife * (hitEvent.Killed ? 1.4f : 1f);

            // spawn above the hit point, with a random offset so multiple hits don't overlap
            number.Start = position + NumberOffset + new Vector2(0, _numberSpawnRise)
                           + new Vector2(Random.Range(-25f, 25f), Random.Range(-10f, 15f));
            number.Drift = Random.Range(-15f, 15f);
            number.Text.text = Mathf.RoundToInt(hitEvent.Damage).ToString();
            number.Text.color = _numberColors.Pick(hitEvent.Killed, headshot);
            Show(number);
        }

        public void OnDamageTaken(DamageTakenEvent damageEvent)
        {
            if (!CanShowDirection || damageEvent.SourceDirection.sqrMagnitude < 0.0001f) return;

            var indicator = NextIndicator();
            indicator.Age = 0f;
            indicator.Direction = damageEvent.SourceDirection;
            indicator.Strength = Mathf.Clamp01(0.6f + damageEvent.Damage / 60f);
            indicator.Pivot.anchoredPosition = DirectionOffset;
            Show(indicator);
        }

        public void Preview()
        {
            if (CanShowMarker)
            {
                _markerIcon.rectTransform.sizeDelta = HitMarkerSize();
                _markerAge = 0f;
                _markerLife = _markerDuration * 3f;
                _markerPunchScale = 1.2f;
                _markerColor = _theme.Gold;
                _markerRoot.anchoredPosition = MarkerOffset;
            }

            var number = NextNumber();
            number.Age = 0f;
            number.Life = 2f;
            number.Start = NumberOffset + new Vector2(0, _numberSpawnRise);
            number.Drift = 0;
            number.Text.text = "42";
            number.Text.color = _theme.Text;
            Show(number);

            if (CanShowDirection)
            {
                var indicator = NextIndicator();
                indicator.Age = 0f;
                indicator.Direction = Vector3.forward;
                indicator.Strength = 1f;
                Show(indicator);
            }
        }

        public void Update(float deltaTime)
        {
            UpdateMarker(deltaTime);
            UpdateNumbers(deltaTime);
            UpdateIndicators(deltaTime);
        }

        private void UpdateMarker(float deltaTime)
        {
            if (_markerAge > _markerLife)
            {
                if (_markerRoot.gameObject.activeSelf) _markerRoot.gameObject.SetActive(false);
                return;
            }

            if (!_markerRoot.gameObject.activeSelf) _markerRoot.gameObject.SetActive(true);
            _markerRoot.anchoredPosition = MarkerOffset;
            _markerAge += deltaTime;

            var progress = Mathf.Clamp01(_markerAge / _markerLife);
            var ease = 1f - Mathf.Clamp01(progress * 4f);
            var scale = Mathf.Lerp(_markerPunchScale, 1f, 1f - ease * ease * ease);
            _markerRoot.localScale = new Vector3(scale, scale, 1f);

            var color = _markerColor;
            color.a = progress < 0.5f ? 1f : 1f - (progress - 0.5f) * 2f;
            _markerIcon.color = color;
        }

        private void UpdateNumbers(float deltaTime)
        {
            if (_liveNumbers == 0) return;

            foreach (var number in _numbers)
            {
                if (!number.Text.gameObject.activeSelf) continue;

                if (number.Age > number.Life)
                {
                    number.Text.gameObject.SetActive(false);
                    _liveNumbers--;
                    continue;
                }

                number.Age += deltaTime;
                var progress = Mathf.Clamp01(number.Age / number.Life);
                var rise = progress * (2f - progress); // ease out
                number.Text.rectTransform.anchoredPosition =
                    number.Start + new Vector2(number.Drift * progress, _numberRise * rise);

                // fully visible for the first 60%, then fades
                var color = number.Text.color;
                color.a = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;
                number.Text.color = color;
            }
        }

        private void UpdateIndicators(float deltaTime)
        {
            if (_liveIndicators == 0) return;

            // Camera.main is a lookup, so only do it while something is animating
            var cam = Camera.main;
            var forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            foreach (var indicator in _indicators)
            {
                if (!indicator.Pivot.gameObject.activeSelf) continue;

                if (indicator.Age > _directionLife)
                {
                    indicator.Pivot.gameObject.SetActive(false);
                    _liveIndicators--;
                    continue;
                }

                indicator.Age += deltaTime;
                indicator.Pivot.anchoredPosition = DirectionOffset;

                var flat = Vector3.ProjectOnPlane(indicator.Direction, Vector3.up);
                indicator.Pivot.localRotation = Quaternion.Euler(0, 0, -Vector3.SignedAngle(forward, flat, Vector3.up));

                var color = _directionColor;
                color.a = indicator.Strength * Mathf.Clamp01(1f - indicator.Age / _directionLife);
                indicator.Icon.color = color;
            }
        }

        // Sizes the indicator so the sprite keeps its proportions.
        // An explicit width in the theme wins, and with no sprite we fall back to the old thin-line look.
        private static Vector2 IndicatorSize(XNode node, Sprite sprite, float height)
        {
            var customWidth = node.GetFloat("width", 0f);
            if (customWidth > 0f) return new Vector2(customWidth, height);

            if (sprite != null && sprite.rect.height > 0f)
                return new Vector2(height * sprite.rect.width / sprite.rect.height, height);

            return new Vector2(Mathf.Max(1f, node.GetFloat("thickness", 2f) * 8f), height);
        }

        // round-robin through the pool, oldest slot gets reused
        private Number NextNumber()
        {
            var number = _numbers[_nextNumber];
            _nextNumber = (_nextNumber + 1) % _numbers.Length;
            return number;
        }

        private Indicator NextIndicator()
        {
            var indicator = _indicators[_nextIndicator];
            _nextIndicator = (_nextIndicator + 1) % _indicators.Length;
            return indicator;
        }

        // the live counters only go up when the object wasn't already showing
        private void Show(Number number)
        {
            if (!number.Text.gameObject.activeSelf) _liveNumbers++;
            number.Text.gameObject.SetActive(true);
        }

        private void Show(Indicator indicator)
        {
            if (!indicator.Pivot.gameObject.activeSelf) _liveIndicators++;
            indicator.Pivot.gameObject.SetActive(true);
        }

        private Vector2 HitMarkerSize()
        {
            var custom = Settings.HitMarkerThickness.Value;
            var thickness = custom > 0f ? custom : _markerThickness;
            var size = (2f * (_markerGap + _markerLength) + thickness) * (128f / 84f);
            return new Vector2(size, size);
        }

        private static HitColors ReadColors(XNode node, Theme theme)
        {
            return new HitColors
            {
                Normal = node.GetColor("color", theme.Text),
                Headshot = node.GetColor("headshotColor", theme.Gold),
                Kill = node.GetColor("killColor", theme.Bad)
            };
        }

        private static float PositiveOrDefault(float value, float defaultValue)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : defaultValue;
        }
    }
}