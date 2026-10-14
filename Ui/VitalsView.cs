using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    // The health / energy / hydration block: a body silhouette that changes color per limb,
    internal class VitalsView
    {
        private static readonly string[] SilhouettePartNames =
            { "Head", "Chest", "Stomach", "LeftArm", "RightArm", "LeftLeg", "RightLeg" };

        // Used when the theme doesn't define any <Stat> nodes of its own.
        private const string DefaultStats =
            "<Stats>" +
            "<Stat id='Health' label='HEALTH' x='100' y='128'/>" +
            "<Stat id='Energy' label='ENERGY' x='100' y='70'/>" +
            "<Stat id='Hydration' label='HYDRATION' x='100' y='12'/>" +
            "</Stats>";

        // Below this health ratio, automatically colored stats start to pulse.
        private const float LowHealthPulseThreshold = 0.3f;

        private class StatUi
        {
            public string StatId, Format, Suffix;
            public RectTransform Root;
            public TMP_Text Label;
            public TMP_Text Value;
            public Image Fill;
            public Vector2 Position;
            public ConfigEntry<int> OffsetX, OffsetY;
            public float BarWidth, DisplayedRatio = 1f;
            public bool UseAutomaticColor;
            public Color DisplayColor;
            public bool ThemeLabelEnabled;

            // What's currently on screen. Text is only rebuilt when one of these changes.
            public int LastDisplayedCurrent = int.MinValue;
            public int LastDisplayedMaximum = int.MinValue;
            public int LastDisplayedPercentage = int.MinValue;
        }

        public RectTransform Root { get; }
        public float ThemeScale { get; }
        public bool ThemeShow { get; }

        private readonly Theme _theme;
        private readonly string _dimRichTextColor;

        // Per-limb state, all keyed by body part.
        private readonly Dictionary<EBodyPart, Image> _bodyPartImages = new Dictionary<EBodyPart, Image>();
        private readonly Dictionary<EBodyPart, float> _bodyPartFlashIntensities = new Dictionary<EBodyPart, float>();
        private readonly Dictionary<EBodyPart, float> _displayedBodyPartHealthRatios = new Dictionary<EBodyPart, float>();

        private readonly List<StatUi> _stats = new List<StatUi>();
        private readonly List<string> _bodyPartIssueLabels = new List<string>(7);

        private readonly TMP_Text _status;
        private readonly RectTransform _silhouette;
        private Image _silhouetteBaseImage;
        private Sprite _silhouetteSprite, _painkillerSilhouetteSprite;

        // Positions from the theme. The config offsets get added on top of these.
        private readonly Vector2 _rootPosition;
        private readonly Vector2 _silhouettePosition;
        private readonly Vector2 _statusPosition;
        private readonly ConfigEntry<int> _statusOffsetX, _statusOffsetY;

        private readonly float _silhouetteOpacity, _maskThreshold, _maskSoftness;
        private readonly bool _flashEnabled;
        private readonly bool _themeShowSilhouette;
        private Color _baseColor, _painkillerBaseColor;

        // One bit per body part. Lets us skip rebuilding the status text when nothing changed.
        private int _lastBodyPartIssueMask = -1;

        public VitalsView(Transform parent, Theme theme)
        {
            _theme = theme;
            _dimRichTextColor = ColorUtility.ToHtmlStringRGBA(theme.Dim);

            var settings = theme.Root.GetChild("Vitals");
            ThemeShow = settings.GetBoolean("show", true);
            ThemeScale = settings.GetFloat("scale", 1f);

            var anchor = settings.GetAnchor("anchor", Vector2.zero);
            _rootPosition = new Vector2(settings.GetFloat("x", 40), settings.GetFloat("y", 40));
            Root = UiKit.CreateNode(parent, "Vitals", anchor, anchor, _rootPosition, Vector2.zero);

            var silhouetteSettings = settings.GetChild("Silhouette");
            _silhouetteOpacity = silhouetteSettings.GetFloat("opacity", 0.9f);
            _maskThreshold = silhouetteSettings.GetFloat("maskThreshold", 0.5f);
            _maskSoftness = silhouetteSettings.GetFloat("maskSoftness", 0.12f);
            _flashEnabled = silhouetteSettings.GetBoolean("flash", true);
            _themeShowSilhouette = silhouetteSettings.GetBoolean("show", true);
            _silhouettePosition = new Vector2(silhouetteSettings.GetFloat("x", 0), silhouetteSettings.GetFloat("y", 0));
            _silhouette = _themeShowSilhouette ? BuildSilhouette(silhouetteSettings) : null;

            var statNodes = settings.GetChildren("Stat");

            if (statNodes.Count == 0)
            {
                statNodes = XNode.Parse(DefaultStats).GetChildren("Stat");
            }

            foreach (var statNode in statNodes)
            {
                BuildStat(statNode);
            }

            var statusSettings = settings.GetChild("Status");
            if (statusSettings.GetBoolean("show", true))
            {
                _statusPosition = new Vector2(statusSettings.GetFloat("x", 100), statusSettings.GetFloat("y", -20));
                _status = UiKit.CreateLabel(Root, "Status", "", statusSettings.GetInt("size", 14), theme.Bad,
                    TextAnchor.LowerLeft, Vector2.zero, Vector2.zero, _statusPosition, new Vector2(300, 20));
            }

            _statusOffsetX = Settings.StatusX;
            _statusOffsetY = Settings.StatusY;
        }

        private RectTransform BuildSilhouette(XNode silhouetteSettings)
        {
            var imagePrefix = silhouetteSettings.GetString("prefix", "body");
            var imageSize = new Vector2(silhouetteSettings.GetFloat("width", 86), silhouetteSettings.GetFloat("height", 132));
            var silhouetteRoot = UiKit.CreateNode(Root, "Silhouette", Vector2.zero, Vector2.zero, _silhouettePosition, imageSize);

            _silhouetteSprite = _theme.LoadSprite(silhouetteSettings.GetString("image", imagePrefix + "_silhouette.png"));
            _painkillerSilhouetteSprite = _theme.LoadSprite(silhouetteSettings.GetString("painkillerImage", imagePrefix + "_outline.png"));

            _baseColor = silhouetteSettings.GetColor("baseColor", new Color(1, 1, 1, _maskSoftness));
            _painkillerBaseColor = silhouetteSettings.GetColor("painkillerBaseColor", new Color(1, 1, 1, _maskSoftness * 2f));

            // Fall back to the painkiller art if the normal silhouette is missing.
            var baseSprite = _silhouetteSprite ? _silhouetteSprite : _painkillerSilhouetteSprite;
            if (baseSprite)
            {
                _silhouetteBaseImage = AddStretchedImage(silhouetteRoot, "BaseImage", baseSprite, _baseColor);
            }

            // One overlay image per limb. These are what get tinted by health.
            foreach (var partName in SilhouettePartNames)
            {
                if (!Enum.TryParse(partName, out EBodyPart bodyPart)) continue;

                var mask = _theme.LoadPartMask(imagePrefix + "_" + partName + ".png", silhouetteSettings);
                if (!mask) continue;

                _bodyPartImages[bodyPart] = AddStretchedImage(silhouetteRoot, partName, mask, Color.white);
                _bodyPartFlashIntensities[bodyPart] = 0f;
                _displayedBodyPartHealthRatios[bodyPart] = 1f;
            }

            // The outline sits on top. A theme can switch it off with outline="none".
            var outlineFile = silhouetteSettings.GetString("outline", imagePrefix + "_outline.png");
            var outline = outlineFile.Equals("none", StringComparison.OrdinalIgnoreCase)
                ? null
                : _theme.LoadSprite(outlineFile);
            if (outline)
            {
                AddStretchedImage(silhouetteRoot, "Outline", outline,
                    silhouetteSettings.GetColor("outlineColor", new Color(1, 1, 1, 0.3f)));
            }

            return silhouetteRoot;
        }

        private static Image AddStretchedImage(Transform parent, string objectName, Sprite sprite, Color color)
        {
            var image = UiKit.CreateImage(parent, objectName, sprite, color,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiKit.StretchToFillParent(image.rectTransform);
            image.preserveAspect = true;
            return image;
        }

        private void BuildStat(XNode statSettings)
        {
            var statId = statSettings.GetString("id", "Health");
            var position = new Vector2(statSettings.GetFloat("x", 100), statSettings.GetFloat("y", 0));
            var valueFontSize = statSettings.GetInt("size", 34);
            var labelFontSize = statSettings.GetInt("labelSize", 11);

            GetOffsetSettings(statId, out var offsetX, out var offsetY);
            var statRoot = UiKit.CreateNode(Root, statId, Vector2.zero, Vector2.zero, position, Vector2.zero);

            var stat = new StatUi
            {
                StatId = statId,
                Format = statSettings.GetString("format", "{cur}"),
                Suffix = statSettings.GetString("suffix", "/{max}"),
                // Health defaults to "auto" (color follows health). The others default to a fixed color.
                UseAutomaticColor = statSettings.GetString("color", statId == "Health" ? "auto" : "") == "auto",
                DisplayColor = statSettings.GetColor("color", _theme.Text),
                BarWidth = statSettings.GetFloat("barWidth", 90),
                Root = statRoot,
                Position = position,
                OffsetX = offsetX,
                OffsetY = offsetY,
                ThemeLabelEnabled = statSettings.GetBoolean("showLabel", true)
            };

            var labelText = statSettings.GetString("label", statId.ToUpperInvariant());
            stat.Label = UiKit.CreateLabel(statRoot, statId + "Label", labelText, labelFontSize,
                statSettings.GetColor("labelColor", _theme.Dim),
                TextAnchor.LowerLeft, Vector2.zero, Vector2.zero,
                new Vector2(1, valueFontSize * 1.05f), new Vector2(200, 16));

            stat.Value = UiKit.CreateLabel(statRoot, statId + "Value", "", valueFontSize, stat.DisplayColor,
                TextAnchor.LowerLeft, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(240, valueFontSize + 6));

            var barHeight = statSettings.GetFloat("barHeight", 2f);
            if (barHeight > 0)
            {
                var backgroundSprite = _theme.LoadThemeSprite(
                    statSettings.GetString("barBackgroundSprite", "bar_background.png"));
                var fillSprite = _theme.LoadThemeSprite(
                    statSettings.GetString("barFillSprite", "bar_fill.png"));
                if (backgroundSprite && fillSprite)
                {
                    var barBackground = UiKit.CreateImage(statRoot, statId + "BarBack", backgroundSprite,
                        statSettings.GetColor("barBackgroundColor", new Color(1, 1, 1, 0.12f)),
                        Vector2.zero, Vector2.zero,
                        new Vector2(2, -4), new Vector2(stat.BarWidth, barHeight));

                    stat.Fill = UiKit.CreateImage(barBackground.transform, "Fill", fillSprite,
                        stat.DisplayColor, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                    UiKit.StretchToFillParent(stat.Fill.rectTransform);
                    stat.Fill.type = Image.Type.Filled;
                    stat.Fill.fillMethod = Image.FillMethod.Horizontal;
                    stat.Fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                }
            }

            _stats.Add(stat);
        }

        private static void GetOffsetSettings(
            string statId, out ConfigEntry<int> offsetX, out ConfigEntry<int> offsetY)
        {
            switch (statId)
            {
                case "Health":
                    offsetX = Settings.HealthX;
                    offsetY = Settings.HealthY;
                    break;
                case "Energy":
                    offsetX = Settings.EnergyX;
                    offsetY = Settings.EnergyY;
                    break;
                case "Hydration":
                    offsetX = Settings.HydrationX;
                    offsetY = Settings.HydrationY;
                    break;
                default:
                    // Custom stats from a theme don't have config offsets.
                    offsetX = null;
                    offsetY = null;
                    break;
            }
        }

        public void OnDamage(DamageTakenEvent damageEvent)
        {
            if (_bodyPartFlashIntensities.ContainsKey(damageEvent.Part))
                _bodyPartFlashIntensities[damageEvent.Part] = 1f;
        }

        public void Reset()
        {
            // Copy the keys first so we're not changing the dictionary while looping over it.
            foreach (var bodyPart in new List<EBodyPart>(_bodyPartFlashIntensities.Keys))
                _bodyPartFlashIntensities[bodyPart] = 0f;

            _lastBodyPartIssueMask = -1;
        }

        // Applies theme + config offsets to the root, silhouette, status and every stat,
        private void ApplyLayout()
        {
            SetPosition(Root, _rootPosition + new Vector2(Settings.VitalsX.Value, Settings.VitalsY.Value));

            if (_silhouette)
            {
                _silhouette.anchoredPosition =
                    _silhouettePosition + new Vector2(Settings.SilhouetteX.Value, Settings.SilhouetteY.Value);
                SetActive(_silhouette.gameObject, _themeShowSilhouette && Settings.ShowSilhouette.Value);
            }

            if (_status)
            {
                SetPosition(_status.rectTransform,
                    _statusPosition + new Vector2(_statusOffsetX.Value, _statusOffsetY.Value));
            }

            foreach (var stat in _stats)
            {
                if (stat.OffsetX != null && stat.OffsetY != null)
                    SetPosition(stat.Root, stat.Position + new Vector2(stat.OffsetX.Value, stat.OffsetY.Value));

                SetActive(stat.Label.gameObject, stat.ThemeLabelEnabled && IsLabelEnabled(stat.StatId));
            }
        }

        // Fills the HUD with fake numbers so the user can line things up in the settings menu.
        public void Preview()
        {
            ApplyLayout();
            _lastBodyPartIssueMask = -1;

            foreach (var stat in _stats)
            {
                var isHealth = stat.StatId == "Health";
                var current = isHealth ? 438 : 78;
                var maximum = isHealth ? 480 : 100;
                var ratio = (float)current / maximum;

                if (stat.Value.color != stat.DisplayColor) stat.Value.color = stat.DisplayColor;
                UpdateStatText(stat, current, maximum, Mathf.RoundToInt(ratio * 100f), false);

                // Force the next live update to redraw the text instead of trusting the preview.
                stat.LastDisplayedCurrent = int.MinValue;
                stat.LastDisplayedMaximum = int.MinValue;
                stat.LastDisplayedPercentage = int.MinValue;

                if (stat.Fill)
                {
                    stat.Fill.fillAmount = ratio;
                    stat.Fill.color = stat.DisplayColor;
                }
            }

            foreach (var bodyPartImage in _bodyPartImages)
                bodyPartImage.Value.color = _theme.HealthColor(UnityEngine.Random.Range(0.1f, 1f));

            if (_status) _status.text = "R.ARM BROKEN";
        }

        public void Update(Player player, float deltaTime)
        {
            ApplyLayout();

            var healthController = player.HealthController;
            UpdatePainkillerSilhouette(healthController.GetAllActiveEffects());

            // Frame-rate independent smoothing, so bars ease the same at 60 or 144 fps.
            var smoothing = 1f - Mathf.Exp(-12f * deltaTime);

            UpdateStats(healthController, smoothing);
            UpdateBodyParts(healthController, smoothing, deltaTime);
        }

        private void UpdateStats(IHealthController healthController, float smoothing)
        {
            foreach (var stat in _stats)
            {
                var statHealth = stat.StatId == "Energy" ? healthController.Energy
                    : stat.StatId == "Hydration" ? healthController.Hydration
                    : healthController.GetBodyPartHealth(EBodyPart.Common);

                var ratio = statHealth.Maximum > 0 ? statHealth.Current / statHealth.Maximum : 0f;
                var current = Mathf.CeilToInt(statHealth.Current);
                var maximum = Mathf.CeilToInt(statHealth.Maximum);
                var percentage = Mathf.RoundToInt(ratio * 100f);

                var color = stat.UseAutomaticColor ? _theme.HealthColor(ratio) : stat.DisplayColor;
                if (stat.UseAutomaticColor && ratio > 0f && ratio < LowHealthPulseThreshold)
                    color.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);

                bool valuesChanged = current != stat.LastDisplayedCurrent
                                     || maximum != stat.LastDisplayedMaximum
                                     || percentage != stat.LastDisplayedPercentage;
                if (valuesChanged) UpdateStatText(stat, current, maximum, percentage, true);

                if (stat.Value.color != color) stat.Value.color = color;

                if (stat.Fill)
                {
                    stat.DisplayedRatio = Mathf.Lerp(stat.DisplayedRatio, ratio, smoothing);
                    stat.Fill.fillAmount = Mathf.Clamp01(stat.DisplayedRatio);
                    if (stat.Fill.color != color) stat.Fill.color = color;
                }
            }
        }

        private void UpdateBodyParts(IHealthController healthController, float smoothing, float deltaTime)
        {
            var issueMask = 0;
            var issueBit = 1;

            // The dictionary is filled once in BuildSilhouette and never changed afterwards,
            // so each body part keeps the same bit between frames.
            foreach (var entry in _bodyPartImages)
            {
                var bodyPart = entry.Key;
                var partHealth = healthController.GetBodyPartHealth(bodyPart);
                var ratio = partHealth.Maximum > 0 ? partHealth.Current / partHealth.Maximum : 0f;

                _displayedBodyPartHealthRatios[bodyPart] =
                    Mathf.Lerp(_displayedBodyPartHealthRatios[bodyPart], ratio, smoothing);
                _bodyPartFlashIntensities[bodyPart] =
                    Mathf.MoveTowards(_bodyPartFlashIntensities[bodyPart], 0f, deltaTime * 2.5f);

                var isDestroyed = healthController.IsBodyPartDestroyed(bodyPart);
                var color = isDestroyed
                    ? _theme.Lost
                    : _theme.HealthColor(_displayedBodyPartHealthRatios[bodyPart]);

                if (_flashEnabled)
                    color = Color.Lerp(color, Color.white, _bodyPartFlashIntensities[bodyPart] * 0.85f);

                color.a = _silhouetteOpacity;
                if (entry.Value.color != color) entry.Value.color = color;

                if (isDestroyed || healthController.IsBodyPartBroken(bodyPart))
                    issueMask |= issueBit;
                issueBit <<= 1;
            }

            if (_status && issueMask != _lastBodyPartIssueMask)
            {
                _lastBodyPartIssueMask = issueMask;
                RefreshStatusText(healthController);
            }
        }

        private void RefreshStatusText(IHealthController healthController)
        {
            _bodyPartIssueLabels.Clear();

            foreach (var entry in _bodyPartImages)
            {
                var bodyPart = entry.Key;
                if (healthController.IsBodyPartDestroyed(bodyPart))
                    _bodyPartIssueLabels.Add(Name(bodyPart) + " LOST");
                else if (healthController.IsBodyPartBroken(bodyPart))
                    _bodyPartIssueLabels.Add(Name(bodyPart) + " BROKEN");
            }

            var text = string.Join("  ", _bodyPartIssueLabels);
            if (_status.text != text) _status.text = text;
        }

        private void UpdatePainkillerSilhouette(IEnumerable<IHealthEffect> activeEffects)
        {
            if (!_silhouetteBaseImage) return;

            var hasPainkiller = false;
            foreach (var effect in activeEffects)
            {
                if (effect is IPainKiller)
                {
                    hasPainkiller = true;
                    break;
                }
            }

            var usePainkiller = hasPainkiller && _painkillerSilhouetteSprite;
            var sprite = usePainkiller ? _painkillerSilhouetteSprite : _silhouetteSprite;
            var color = usePainkiller ? _painkillerBaseColor : _baseColor;

            if (sprite && _silhouetteBaseImage.sprite != sprite) _silhouetteBaseImage.sprite = sprite;
            if (_silhouetteBaseImage.color != color) _silhouetteBaseImage.color = color;
        }

        private void UpdateStatText(StatUi stat, int current, int maximum, int percentage, bool dimSuffix)
        {
            // Health changes constantly, but the rounded numbers dont
            stat.LastDisplayedCurrent = current;
            stat.LastDisplayedMaximum = maximum;
            stat.LastDisplayedPercentage = percentage;

            var currentText = current.ToString();
            var maximumText = maximum.ToString();

            var mainText = stat.Format
                .Replace("{cur}", currentText)
                .Replace("{max}", maximumText)
                .Replace("{pct}", percentage.ToString());

            var suffix = stat.Suffix
                .Replace("{cur}", currentText)
                .Replace("{max}", maximumText);

            // The suffix ("/480") is drawn smaller and dimmer
            if (dimSuffix && suffix.Length > 0)
            {
                var suffixSize = Mathf.Max(10, stat.Value.fontSize / 2);
                suffix = "<size=" + suffixSize + "><color=#" + _dimRichTextColor + ">" + suffix + "</color></size>";
            }

            var displayText = mainText + suffix;
            if (stat.Value.text != displayText) stat.Value.text = displayText;
        }

        // Avoids marking the canvas dirty every frame.
        private static void SetPosition(RectTransform rect, Vector2 position)
        {
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }

        private static bool IsLabelEnabled(string statId)
        {
            switch (statId)
            {
                case "Health": return Settings.ShowHealthLabel.Value;
                case "Hydration": return Settings.ShowHydrationLabel.Value;
                case "Energy": return Settings.ShowEnergyLabel.Value;
                default: return true;
            }
        }

        public static string Name(EBodyPart part)
        {
            switch (part)
            {
                case EBodyPart.Chest: return "THORAX";
                case EBodyPart.LeftArm: return "L.ARM";
                case EBodyPart.RightArm: return "R.ARM";
                case EBodyPart.LeftLeg: return "L.LEG";
                case EBodyPart.RightLeg: return "R.LEG";
                default: return part.ToString().ToUpperInvariant();
            }
        }
    }
}