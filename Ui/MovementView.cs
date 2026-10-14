using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal class MovementView
    {
        private enum Stance
        {
            Standing,
            Walk,
            Crouch,
            Prone,
            Sprint
        }

        private static readonly string[] DefaultSpeedIcons =
            { "volume_0.png", "volume_1.png", "volume_2.png", "volume_4.png", "volume_5.png", "volume_5.png" };

        private const int RunTier = 8;           // km/h from here on counts as running
        private const float LowStamina = 0.2f;   // bar turns red below this

        public RectTransform Root { get; }
        public float ThemeScale { get; }
        public bool ThemeShow { get; }

        private readonly Theme _theme;
        private readonly XNode _settings;
        private readonly Vector2 _position;
        private readonly bool _textOnly;
        private readonly bool _showStanceText, _showSpeedText, _showStaminaLabels;

        private readonly TMP_Text _stance, _speed, _stamina, _handsStamina;
        private readonly Image _stanceIcon, _speedIcon, _staminaIcon, _handsStaminaIcon;
        private readonly Image _staminaBarBackground, _staminaFill;
        private readonly Image _handsStaminaBarBackground, _handsStaminaFill;

        private readonly string _stanceStandIcon, _stanceWalkIcon, _stanceCrouchIcon, _stanceProneIcon, _stanceSprintIcon;
        private readonly string[] _speedIcons = new string[6];
        private readonly Color _stanceColor, _movementColor, _movementRunColor, _staminaColor, _handStaminaColor;

        private readonly Vector2 _stanceIconPosition, _stanceTextPosition;
        private readonly Vector2 _speedIconPosition, _speedTextPosition;
        private readonly Vector2 _staminaIconPosition, _staminaTextPosition, _staminaBarPosition;
        private readonly Vector2 _handsIconPosition, _handsTextPosition, _handsBarPosition;

        private int _speedTier;
        private int _lastSpeedTier = -1;
        private bool _lastSprinting;
        private string _lastSpeedIcon, _lastStanceIcon;
        private int _lastLegCurrent = int.MinValue, _lastLegMax = int.MinValue;
        private int _lastHandCurrent = int.MinValue, _lastHandMax = int.MinValue;

        public MovementView(Transform parent, Theme theme)
        {
            _theme = theme;
            _settings = theme.Root.GetChild("Movement");
            _textOnly = !theme.IconsEnabled;

            _stanceColor = theme.IconColor("stance", theme.Good);
            _movementColor = theme.IconColor("movement", theme.Warn);
            _movementRunColor = theme.IconColor("movementRun", theme.Warn);
            _staminaColor = theme.IconColor("stamina", theme.Good);
            _handStaminaColor = theme.IconColor("handsStamina", theme.Warn);

            ThemeShow = _settings.GetBoolean("show", true);
            ThemeScale = _settings.GetFloat("scale", 1f);
            var anchor = _settings.GetAnchor("anchor", Vector2.zero);
            _position = new Vector2(_settings.GetFloat("x", 28), _settings.GetFloat("y", 28));
            Root = UiKit.CreateNode(parent, "Movement", anchor, anchor, _position, Vector2.zero);

            _showStanceText = _settings.GetBoolean("showStanceText", _textOnly);
            _showSpeedText = _settings.GetBoolean("showSpeedText", _textOnly);
            _showStaminaLabels = _settings.GetBoolean("showStaminaLabels", _textOnly);

            _stanceStandIcon = _settings.GetString("stanceIcon", "stance_stand.png");
            _stanceCrouchIcon = _settings.GetString("crouchIcon", "stance_crouch.png");
            _stanceProneIcon = _settings.GetString("proneIcon", "stance_prone.png");
            _stanceSprintIcon = _settings.GetString("sprintIcon", "stance_sprint.png");

            // themes that don't ship a walk icon keep showing the standing one
            var walkFile = _settings.GetString("walkIcon", "stance_walk.png");
            _stanceWalkIcon = IconExists(walkFile) ? walkFile : _stanceStandIcon;
            for (var index = 0; index < _speedIcons.Length; index++)
                _speedIcons[index] = _settings.GetString("speedIcon" + index, DefaultSpeedIcons[index]);

            var iconSize = _settings.GetFloat("iconSize", 19);
            var stanceSize = _settings.GetFloat("stanceIconSize", iconSize);
            var speedSize = _settings.GetFloat("speedIconSize", 14);
            var staminaIconSize = _settings.GetFloat("staminaIconSize", iconSize);
            var handsIconSize = _settings.GetFloat("handsStaminaIconSize", staminaIconSize);
            var textOffset = _textOnly ? 0 : stanceSize + 7;

            // icons have their own x/y keys and fall back to the text ones
            var stanceX = _settings.GetFloat("stanceX", 0);
            var stanceY = _settings.GetFloat("stanceY", 58);
            _stanceIconPosition = new Vector2(stanceX, stanceY);
            _stanceTextPosition = new Vector2(textOffset + stanceX, stanceY);

            _speedIconPosition = new Vector2(_settings.GetFloat("speedX", stanceSize + 10), _settings.GetFloat("speedY", 58));
            _speedTextPosition = new Vector2(textOffset + _settings.GetFloat("speedX", 0), _settings.GetFloat("speedY", 34));

            var staminaX = _settings.GetFloat("staminaX", 0);
            var staminaY = _settings.GetFloat("staminaY", 20);
            _staminaIconPosition = new Vector2(_settings.GetFloat("staminaIconX", staminaX),
                _settings.GetFloat("staminaIconY", staminaY));
            _staminaTextPosition = new Vector2(textOffset + staminaX, staminaY);
            _staminaBarPosition = new Vector2(_settings.GetFloat("staminaBarX", 0), _settings.GetFloat("staminaBarY", 4));

            var handsX = _settings.GetFloat("handsStaminaX", 0);
            var handsY = _settings.GetFloat("handsStaminaY", -14);
            _handsIconPosition = new Vector2(_settings.GetFloat("handsStaminaIconX", handsX),
                _settings.GetFloat("handsStaminaIconY", handsY));
            _handsTextPosition = new Vector2(textOffset + handsX, handsY);
            _handsBarPosition = new Vector2(_settings.GetFloat("handStaminaBarX", 0), _settings.GetFloat("handStaminaBarY", -30));

            var labelSize = _settings.GetInt("fontSize", 13);
            var staminaFontSize = _settings.GetInt("fontSize", 12);

            _stanceIcon = Icon("stanceIcon", "stance_stand.png", _stanceIconPosition, stanceSize, _stanceColor);
            _stance = Label("Stance", _stanceTextPosition, labelSize, theme.Text, new Vector2(180, 22));
            _stance.gameObject.SetActive(_showStanceText);

            _speedIcon = Icon("speedIcon", _speedIcons[0], _speedIconPosition, speedSize, _movementColor);
            _speed = Label("Speed", _speedTextPosition, labelSize, theme.Text, new Vector2(180, 22));
            _speed.gameObject.SetActive(_showSpeedText);

            _staminaIcon = Icon("staminaIcon", "stamina.png", _staminaIconPosition, staminaIconSize, _staminaColor);
            _handsStaminaIcon = Icon("handsStaminaIcon", "hand_stamina.png", _handsIconPosition, handsIconSize,
                _handStaminaColor);
            _stamina = Label("Stamina", _staminaTextPosition, staminaFontSize, theme.Text, new Vector2(180, 20));

            // text-only themes show the numbers instead of bars, with a wider box for the hands text
            var handsBox = new Vector2(_textOnly ? 230 : 180, 20);
            _handsStamina = Label("HandsStamina", _handsTextPosition, staminaFontSize, theme.Text, handsBox);
            if (!_textOnly)
            {
                var barWidth = _settings.GetFloat("staminaBarWidth", 100);
                var barHeight = _settings.GetFloat("staminaBarHeight", 6);

                _staminaBarBackground = Bar("StaminaBack", _staminaBarPosition, barWidth, barHeight,
                    _staminaColor, out _staminaFill);
                _handsStaminaBarBackground = Bar("HandsStaminaBack", _handsBarPosition, barWidth, barHeight,
                    _handStaminaColor, out _handsStaminaFill);
            }

            ApplyFeatureToggles();
            ApplyElementPositions();
        }

        private Image Bar(string name, Vector2 position, float width, float height, Color fillColor, out Image fill)
        {
            var backgroundSprite = _theme.LoadThemeSprite(
                _settings.GetString("staminaBarBackgroundSprite", "bar_background.png"));
            var fillSprite = _theme.LoadThemeSprite(
                _settings.GetString("staminaBarFillSprite", "bar_fill.png"));
            if (!backgroundSprite || !fillSprite)
            {
                fill = null;
                return null;
            }

            var background = UiKit.CreateImage(Root, name, backgroundSprite,
                _settings.GetColor("staminaBarBackColor", new Color(1, 1, 1, 0.32f)), Vector2.zero,
                Vector2.zero, position, new Vector2(width, height));
            fill = UiKit.CreateImage(background.transform, name + "Fill", fillSprite, fillColor,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiKit.StretchToFillParent(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            return background;
        }

        private Image Icon(string key, string fallback, Vector2 position, float size, Color color)
        {
            if (!_theme.IconsEnabled) return null;

            var file = _settings.GetString(key, fallback);
            var sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
            if (!sprite) return null;

            var image = UiKit.CreateImage(Root, key, sprite, color, Vector2.zero, Vector2.zero, position,
                new Vector2(size, size));
            image.preserveAspect = true;
            return image;
        }

        private TMP_Text Label(string name, Vector2 position, int size, Color color, Vector2 box)
        {
            return UiKit.CreateLabel(Root, name, "", size, color, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero,
                position, box);
        }

        public void Update(EFT.Player player, float deltaTime)
        {
            Layout();

            // speed comes first, "walking" is just standing while actually moving
            _speedTier = GetSpeedTier(player.MovementContext.ActualLinearVelocity * 3.6f, _speedTier);
            var stance = GetStance(player, _speedTier > 0);
            UpdateStance(stance);
            UpdateSpeed(stance == Stance.Sprint);
            UpdateStamina(player);
        }

        public void Preview()
        {
            Layout();
            var standing = TextLocalization.Get("movement.stance.standing");
            if (_stance.text != standing) _stance.text = standing;
            ShowStamina(_stamina, TextLocalization.Get("movement.stamina.leg"),
                72, 100, 0.72f, _staminaFill, _staminaColor,
                ref _lastLegCurrent, ref _lastLegMax);
            ShowStamina(_handsStamina, TextLocalization.Get("movement.stamina.hand"),
                46, 100, 0.46f, _handsStaminaFill, _handStaminaColor,
                ref _lastHandCurrent, ref _lastHandMax);
        }

        private void Layout()
        {
            SetPosition(Root, _position, Settings.MovementX.Value, Settings.MovementY.Value);
            ApplyFeatureToggles();
            ApplyElementPositions();
        }

        // same lookup order as Theme.LoadIconSprite: the theme's own folder, then Minimal
        private bool IconExists(string file)
        {
            var themesFolder = Path.GetDirectoryName(_theme.DirectoryPath);
            return File.Exists(Path.Combine(_theme.DirectoryPath, "Icons", file))
                   || File.Exists(Path.Combine(themesFolder, "Minimal", "Icons", file));
        }

        private static Stance GetStance(EFT.Player player, bool moving)
        {
            if (player.IsSprintEnabled) return Stance.Sprint;
            if (player.IsInPronePose) return Stance.Prone;
            if (player.MovementContext.PoseLevel < 0.5f) return Stance.Crouch;
            return moving ? Stance.Walk : Stance.Standing;
        }

        private void UpdateStance(Stance stance)
        {
            if (_showStanceText)
            {
                var text = stance == Stance.Sprint ? TextLocalization.Get("movement.stance.sprint")
                    : stance == Stance.Prone ? TextLocalization.Get("movement.stance.prone")
                    : stance == Stance.Crouch ? TextLocalization.Get("movement.stance.crouch")
                    : stance == Stance.Walk ? TextLocalization.Get("movement.stance.walking")
                    : TextLocalization.Get("movement.stance.standing");
                if (_stance.text != text) _stance.text = text;
            }

            if (!_stanceIcon) return;

            var file = stance == Stance.Sprint ? _stanceSprintIcon
                : stance == Stance.Prone ? _stanceProneIcon
                : stance == Stance.Crouch ? _stanceCrouchIcon
                : stance == Stance.Walk ? _stanceWalkIcon
                : _stanceStandIcon;
            if (file != _lastStanceIcon)
            {
                _lastStanceIcon = file;
                _stanceIcon.sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
            }
            if (_stanceIcon.color != _stanceColor) _stanceIcon.color = _stanceColor;
        }

        private void UpdateSpeed(bool sprinting)
        {
            // text only depends on the tier and sprint state, not the raw velocity
            if (_speedTier != _lastSpeedTier || sprinting != _lastSprinting)
            {
                _lastSpeedTier = _speedTier;
                _lastSprinting = sprinting;
                if (_speedTier == 0)
                {
                    _speed.text = TextLocalization.Get("movement.speed.stop");
                }
                else
                {
                    var mode = TextLocalization.Get(sprinting || _speedTier >= RunTier
                        ? "movement.speed.sprint"
                        : "movement.speed.move");
                    _speed.text = TextLocalization.Get("movement.speed.format")
                        .Replace("{mode}", mode)
                        .Replace("{speed}", _speedTier.ToString())
                        .Replace("{unit}", TextLocalization.Get("movement.speed.unit"));
                }
            }

            if (!_speedIcon) return;

            var iconIndex = _speedTier == 0 ? 0 : Mathf.Clamp(_speedTier / 2 - 1, 0, _speedIcons.Length - 1);
            var file = _speedIcons[iconIndex];
            if (file != _lastSpeedIcon)
            {
                _lastSpeedIcon = file;
                _speedIcon.sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
            }

            var color = _speedTier >= RunTier ? _movementRunColor : _movementColor;
            if (_speedIcon.color != color) _speedIcon.color = color;
        }

        private void UpdateStamina(EFT.Player player)
        {
            var legs = player.Physical.Stamina;
            var legMax = legs != null ? (float)legs.TotalCapacity : 0f;
            var legCurrent = legs != null ? legs.Current : 0f;

            var hands = player.Physical.HandsStamina;
            var handsMax = hands != null ? (float)hands.TotalCapacity : 0f;
            var handsCurrent = hands != null ? hands.Current : 0f;

            ShowStamina(_stamina, TextLocalization.Get("movement.stamina.leg"), legCurrent, legMax,
                Ratio(legCurrent, legMax), _staminaFill,
                _staminaColor, ref _lastLegCurrent, ref _lastLegMax);
            ShowStamina(_handsStamina, TextLocalization.Get("movement.stamina.hand"),
                handsCurrent, handsMax, Ratio(handsCurrent, handsMax),
                _handsStaminaFill, _handStaminaColor, ref _lastHandCurrent, ref _lastHandMax);
        }

        private static float Ratio(float current, float max)
        {
            return max > 0 ? Mathf.Clamp01(current / max) : 0f;
        }

        // the text shows rounded values, so only rebuild the string when those change
        private void ShowStamina(TMP_Text label, string name, float current, float max, float ratio, Image fill,
            Color fillColor, ref int lastCurrent, ref int lastMax)
        {
            var roundedCurrent = Mathf.CeilToInt(current);
            var roundedMax = Mathf.CeilToInt(max);
            if (roundedCurrent != lastCurrent || roundedMax != lastMax)
            {
                lastCurrent = roundedCurrent;
                lastMax = roundedMax;
                var numbers = roundedCurrent + "/" + roundedMax;
                var text = _showStaminaLabels ? name + "  " + numbers : numbers;
                if (label.text != text) label.text = text;
            }

            if (!fill) return;

            fill.fillAmount = ratio;
            var color = ratio < LowStamina ? _theme.Bad : fillColor;
            if (fill.color != color) fill.color = color;
        }

        private void ApplyElementPositions()
        {
            SetPosition(_stanceIcon, _stanceIconPosition, Settings.MovementStanceX.Value, Settings.MovementStanceY.Value);
            SetPosition(_stance, _stanceTextPosition, Settings.MovementStanceX.Value, Settings.MovementStanceY.Value);
            SetPosition(_speedIcon, _speedIconPosition, Settings.MovementSpeedX.Value, Settings.MovementSpeedY.Value);
            SetPosition(_speed, _speedTextPosition, Settings.MovementSpeedX.Value, Settings.MovementSpeedY.Value);
            SetPosition(_staminaIcon, _staminaIconPosition, Settings.MovementStaminaX.Value, Settings.MovementStaminaY.Value);
            SetPosition(_stamina, _staminaTextPosition, Settings.MovementStaminaX.Value, Settings.MovementStaminaY.Value);
            SetPosition(_staminaBarBackground, _staminaBarPosition, Settings.MovementStaminaBarX.Value, Settings.MovementStaminaBarY.Value);
            SetPosition(_handsStaminaIcon, _handsIconPosition, Settings.MovementHandsX.Value, Settings.MovementHandsY.Value);
            SetPosition(_handsStamina, _handsTextPosition, Settings.MovementHandsX.Value, Settings.MovementHandsY.Value);
            SetPosition(_handsStaminaBarBackground, _handsBarPosition,
                Settings.MovementHandsBarX.Value, Settings.MovementHandsBarY.Value);
        }

        private void ApplyFeatureToggles()
        {
            SetActive(_stanceIcon, Settings.ShowStance.Value);
            SetActive(_stance, Settings.ShowStance.Value && _showStanceText);
            SetActive(_speedIcon, Settings.ShowSpeedIndicator.Value);
            SetActive(_speed, Settings.ShowSpeedIndicator.Value && _showSpeedText);
            SetActive(_staminaBarBackground, Settings.ShowStaminaBars.Value);
            SetActive(_handsStaminaBarBackground, Settings.ShowStaminaBars.Value);
        }

        private static void SetActive(Graphic graphic, bool active)
        {
            if (graphic && graphic.gameObject.activeSelf != active) graphic.gameObject.SetActive(active);
        }

        private static void SetPosition(Graphic graphic, Vector2 position, float offsetX, float offsetY)
        {
            if (graphic) SetPosition(graphic.rectTransform, position, offsetX, offsetY);
        }

        private static void SetPosition(RectTransform rectTransform, Vector2 position, float offsetX, float offsetY)
        {
            var target = position + new Vector2(offsetX, offsetY);
            if (rectTransform.anchoredPosition != target) rectTransform.anchoredPosition = target;
        }

        // Rounds to even km/h steps and holds the old tier near a boundary so the text doesn't flicker
        private static int GetSpeedTier(float speed, int previousTier)
        {
            if (speed < 0.8f) return 0;

            var tier = Mathf.Clamp(Mathf.RoundToInt(speed / 2f) * 2, 2, 16);
            if (previousTier <= 0 || tier == previousTier) return tier;

            const float lagRange = 0.35f;
            var midpoint = (previousTier + tier) * 0.5f;
            if (tier > previousTier && speed < midpoint + lagRange) return previousTier;
            if (tier < previousTier && speed > midpoint - lagRange) return previousTier;
            return tier;
        }
    }
}