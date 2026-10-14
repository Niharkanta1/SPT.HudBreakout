using System;
using System.Collections.Generic;
using System.IO;
using EFT;
using EFT.HealthSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal class EffectsView
    {
        // order here is the order rows are created in
        private enum StatusEffect
        {
            LightBleeding,
            HeavyBleeding,
            FreshWound,
            Fracture,
            Pain,
            HandsTremor,
            MildMusclePain,
            SevereMusclePain,
            Concussion,
            TunnelVision,
            Dehydration,
            EnergyFatigue,
            Overweight,
            CriticalOverweight,
            Fatigue,
            Stun,
            Flash,
            Painkillers,
            Buff,
            Debuff,
            Berserk,
            Poisoning,
            PanicAttack,
            Count
        }

        private sealed class EffectRow
        {
            public RectTransform Root;
            public TMP_Text Text;
            public string Label;
            public int LastCount = int.MinValue;
        }

        public RectTransform Root { get; }
        public float ThemeScale { get; }
        public bool ThemeShow { get; }


        private readonly Theme _theme;
        private readonly bool _colorOverlay;
        private readonly Vector2 _position;
        private readonly List<EffectRow> _rows = new List<EffectRow>((int)StatusEffect.Count);
        private readonly int[] _counts = new int[(int)StatusEffect.Count];
        private readonly float _rowHeight, _iconSize;
        private readonly int _fontSize;

        public EffectsView(Transform parent, Theme theme)
        {
            _theme = theme;
            var settings = theme.Root.GetChild("Effects");
            ThemeShow = settings.GetBoolean("show", true);
            ThemeScale = settings.GetFloat("scale", 1f);
            _rowHeight = settings.GetFloat("rowHeight", 22);
            _iconSize = settings.GetFloat("iconSize", 16);
            _fontSize = settings.GetInt("fontSize", 13);
            _colorOverlay = settings.GetBoolean("colorOverlay", true);

            var anchor = settings.GetAnchor("anchor", new Vector2(0, 1));
            _position = new Vector2(settings.GetFloat("x", 260), settings.GetFloat("y", -38));
            Root = UiKit.CreateNode(parent, "Effects", anchor, anchor, _position, Vector2.zero);

            AddRow(settings, StatusEffect.LightBleeding, "lightBleed", "effects.light_bleeding", theme.Bad);
            AddRow(settings, StatusEffect.HeavyBleeding, "heavyBleed", "effects.heavy_bleeding", theme.Bad);
            AddRow(settings, StatusEffect.FreshWound, "freshWound", "effects.fresh_wound", theme.Bad);
            AddRow(settings, StatusEffect.Fracture, "fracture", "effects.fracture", theme.Warn);
            AddRow(settings, StatusEffect.Pain, "pain", "effects.pain", theme.Gold);
            AddRow(settings, StatusEffect.HandsTremor, "handsTremor", "effects.hands_tremor", theme.Warn);
            AddRow(settings, StatusEffect.MildMusclePain, "mildMusclePain", "effects.mild_muscle_pain", theme.Warn);
            AddRow(settings, StatusEffect.SevereMusclePain, "severeMusclePain", "effects.severe_muscle_pain", theme.Bad);
            AddRow(settings, StatusEffect.Concussion, "concussion", "effects.concussion", theme.Warn);
            AddRow(settings, StatusEffect.TunnelVision, "tunnelVision", "effects.tunnel_vision", theme.Warn);
            AddRow(settings, StatusEffect.Dehydration, "dehydration", "effects.dehydration", theme.Warn);
            AddRow(settings, StatusEffect.EnergyFatigue, "energyFatigue", "effects.energy_fatigue", theme.Warn);
            AddRow(settings, StatusEffect.Overweight, "overweight", "effects.overweight", theme.Warn);
            AddRow(settings, StatusEffect.CriticalOverweight, "criticalOverweight", "effects.critical_overweight", theme.Bad);
            AddRow(settings, StatusEffect.Fatigue, "fatigue", "effects.fatigue", theme.Warn);
            AddRow(settings, StatusEffect.Stun, "stun", "effects.stun", theme.Bad);
            AddRow(settings, StatusEffect.Flash, "flash", "effects.flash", theme.Warn);
            AddRow(settings, StatusEffect.Painkillers, "painkillers", "effects.painkillers", theme.Gold);
            AddRow(settings, StatusEffect.Buff, "buff", "effects.buff", theme.Good);
            AddRow(settings, StatusEffect.Debuff, "debuff", "effects.debuff", theme.Bad);
            AddRow(settings, StatusEffect.Berserk, "berserk", "effects.berserk", theme.Bad);
            AddRow(settings, StatusEffect.Poisoning, "poisoning", "effects.poisoning", theme.Bad);
            AddRow(settings, StatusEffect.PanicAttack, "panicAttack", "effects.panic_attack", theme.Bad);
        }

        private void AddRow(XNode settings, StatusEffect effect, string key, string labelKey, Color color)
        {
            var label = TextLocalization.Get(labelKey);
            var rowIndex = (int)effect;
            var row = UiKit.CreateNode(Root, label, Vector2.zero, Vector2.zero,
                new Vector2(0, -rowIndex * _rowHeight), new Vector2(190, _rowHeight));

            var iconColor = _colorOverlay ? color : _theme.Default;

            if (_theme.IconsEnabled)
            {
                var file = settings.GetString(key + "Icon", key + ".png");
                var sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
                if (sprite)
                {
                    var icon = UiKit.CreateImage(row, "Icon", sprite, _theme.IconColor(key, iconColor),
                        Vector2.zero, Vector2.zero, new Vector2(0, 2), new Vector2(_iconSize, _iconSize));
                    icon.preserveAspect = true;
                }
            }

            var textX = _theme.IconsEnabled ? _iconSize + 7 : 0;
            var text = UiKit.CreateLabel(row, "Text", "", _fontSize, color, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, new Vector2(textX, 0), new Vector2(180, _rowHeight));
            _rows.Add(new EffectRow { Root = row, Text = text, Label = label });
        }

        public void Update(Player player)
        {
            PlaceRoot();
            Array.Clear(_counts, 0, _counts.Length);

            foreach (var effect in player.HealthController.GetAllActiveEffects())
            {
                if (effect is IStimulator stimulator) CountBuffs(stimulator);

                var row = GetStatusEffectRow(effect);
                if (row >= 0) _counts[row]++;
            }

            for (var index = 0; index < _rows.Count; index++)
                SetRow(index, _counts[index]);
            LayoutRows();
        }

        public void Preview()
        {
            PlaceRoot();
            for (var index = 0; index < _rows.Count; index++)
                SetRow(index, 1);
            LayoutRows();
        }

        private void PlaceRoot()
        {
            SetPosition(Root, _position + new Vector2(Settings.EffectsX.Value, Settings.EffectsY.Value));
        }

        // stimulators can carry several buffs/debuffs at once
        private void CountBuffs(IStimulator stimulator)
        {
            var buffs = stimulator.ActiveBuffs;
            if (buffs == null) return;

            foreach (var buff in buffs)
            {
                if (buff == null || !buff.Active) continue;

                var row = buff.Settings != null && buff.Settings.IsBuff ? StatusEffect.Buff : StatusEffect.Debuff;
                _counts[(int)row]++;
            }
        }

        // visible rows stack up with no gaps, plus whatever offsets the player set in the config
        private void LayoutRows()
        {
            var slot = 0;
            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index];
                if (!row.Root.gameObject.activeSelf) continue;

                SetPosition(row.Root, new Vector2(0, -slot * _rowHeight) + UserOffset((StatusEffect)index));
                slot++;
            }
        }

        // only a few rows have their own X/Y entries in the config
        private static Vector2 UserOffset(StatusEffect effect)
        {
            switch (effect)
            {
                case StatusEffect.LightBleeding: return new Vector2(Settings.EffectsLightX.Value, Settings.EffectsLightY.Value);
                case StatusEffect.HeavyBleeding: return new Vector2(Settings.EffectsHeavyX.Value, Settings.EffectsHeavyY.Value);
                case StatusEffect.Fracture: return new Vector2(Settings.EffectsFractureX.Value, Settings.EffectsFractureY.Value);
                case StatusEffect.Pain: return new Vector2(Settings.EffectsPainX.Value, Settings.EffectsPainY.Value);
                default: return Vector2.zero;
            }
        }

        private void SetRow(int index, int count)
        {
            var row = _rows[index];
            var show = count > 0;
            if (row.Root.gameObject.activeSelf != show) row.Root.gameObject.SetActive(show);
            if (row.LastCount == count) return;

            row.LastCount = count;
            if (!show) return;

            var text = row.Label + (count > 1 ? "  x" + count : "");
            if (row.Text.text != text) row.Text.text = text;
        }

        // order matters, the first matching interface wins (e.g. painkiller before plain pain)
        private static int GetStatusEffectRow(IHealthEffect effect)
        {
            return effect switch
            {
                IHeavyBleeding => (int)StatusEffect.HeavyBleeding,
                ILightBleeding => (int)StatusEffect.LightBleeding,
                IWound => (int)StatusEffect.FreshWound,
                IFracture => (int)StatusEffect.Fracture,
                IPainKiller => (int)StatusEffect.Painkillers,
                IPain => (int)StatusEffect.Pain,
                ITremor => (int)StatusEffect.HandsTremor,
                IMildMusclePain => (int)StatusEffect.MildMusclePain,
                ISevereMusclePain => (int)StatusEffect.SevereMusclePain,
                IContusion => (int)StatusEffect.Concussion,
                ITunnelVision => (int)StatusEffect.TunnelVision,
                IDehydration => (int)StatusEffect.Dehydration,
                IExhaustion => (int)StatusEffect.EnergyFatigue,
                IOverEncumbered => (int)StatusEffect.CriticalOverweight,
                IEncumbered => (int)StatusEffect.Overweight,
                IChronicStaminaFatigue => (int)StatusEffect.Fatigue,
                IStun => (int)StatusEffect.Stun,
                IFlash => (int)StatusEffect.Flash,
                IStimulatorBuff buff => buff.Settings?.IsBuff == true ? (int)StatusEffect.Buff : (int)StatusEffect.Debuff,
                IBerserk => (int)StatusEffect.Berserk,
                ILethalIntoxication or IIntoxication or IZombieInfection => (int)StatusEffect.Poisoning,
                IPanicEffect => (int)StatusEffect.PanicAttack,
                _ => -1
            };
        }

        private static void SetPosition(RectTransform rectTransform, Vector2 position)
        {
            if (rectTransform.anchoredPosition != position) rectTransform.anchoredPosition = position;
        }
    }
}