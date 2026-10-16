using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HudBreakout
{
    internal static class Settings
    {
        public static ConfigEntry<bool> ShowVitals, ShowHitMarker, ShowDamageNumbers, ShowKillFeed, ShowDamageDirection;
        public static ConfigEntry<bool> ShowSilhouette, ShowHealthLabel, ShowHydrationLabel, ShowEnergyLabel;
        public static ConfigEntry<bool> ShowAllHud, ShowEffects, ShowEquipment, ShowMovement;
        public static ConfigEntry<bool> ShowStance, ShowSpeedIndicator, ShowWeaponName, ShowAmmo, ShowGrenades;
        public static ConfigEntry<bool> ShowStaminaBars;
        public static ConfigEntry<float> Scale, Opacity, HitMarkerThickness;
        public static ConfigEntry<string> ThemeName, Language;
        public static ConfigEntry<KeyboardShortcut> ReloadKey, PreviewKey;

        public static ConfigEntry<int> VitalsX, VitalsY, SilhouetteX, SilhouetteY;
        public static ConfigEntry<int> HealthX, HealthY, HydrationX, HydrationY, EnergyX, EnergyY;
        public static ConfigEntry<int> StatusX, StatusY;
        public static ConfigEntry<int> EffectsX, EffectsY, EquipmentX, EquipmentY, MovementX, MovementY;

        public static ConfigEntry<int> EffectsLightX, EffectsLightY, EffectsHeavyX, EffectsHeavyY;
        public static ConfigEntry<int> EffectsFractureX, EffectsFractureY, EffectsPainX, EffectsPainY;
        public static ConfigEntry<int> EquipmentWeaponX, EquipmentWeaponY, EquipmentAmmoX, EquipmentAmmoY;
        public static ConfigEntry<int> EquipmentGrenadesX, EquipmentGrenadesY;
        public static ConfigEntry<int> MovementStanceX, MovementStanceY, MovementSpeedX, MovementSpeedY;
        public static ConfigEntry<int> MovementStaminaX, MovementStaminaY, MovementHandsX, MovementHandsY;
        public static ConfigEntry<int> MovementStaminaBarX, MovementStaminaBarY;
        public static ConfigEntry<int> MovementHandsBarX, MovementHandsBarY;
        public static ConfigEntry<int> KillFeedX, KillFeedY, HitMarkerX, HitMarkerY;
        public static ConfigEntry<int> DamageNumbersX, DamageNumbersY, DamageDirectionX, DamageDirectionY;

        private const string Main = "HudBreakout";
        private const string Layout = "Layout";
        private const string Elements = "Element Positions";

        public static void Bind(ConfigFile cfg, string themesDir, string localizationDir)
        {
            TextLocalization.Initialize(localizationDir, "Auto");
            if (cfg.TryGetEntry<string>(new ConfigDefinition(Main, "Language"), out var savedLanguage))
                TextLocalization.SetLanguage(savedLanguage.Value);

            Language = cfg.Bind(Main, "Language", "Auto", new ConfigDescription(
                TextLocalization.Get("settings.language"),
                new AcceptableValueList<string>(TextLocalization.AvailableLanguages),
                DisplayName("settings.name.language")));
            TextLocalization.SetLanguage(Language.Value);

            var themes = GetThemeNames(themesDir);
            if (themes.Length == 0) themes = new[] { "Minimal", "TextOnly" };

            ThemeName = cfg.Bind(Main, "Theme", "Minimal",
                ConfigDescription("settings.name.theme", "settings.theme",
                    new AcceptableValueList<string>(themes)));
            ReloadKey = cfg.Bind(Main, "Reload Theme Key", new KeyboardShortcut(KeyCode.F9),
                ConfigDescription("settings.name.reload_theme_key", "settings.reload_theme_key"));
            PreviewKey = cfg.Bind(Main, "Preview HUD Key", new KeyboardShortcut(KeyCode.F10),
                ConfigDescription("settings.name.preview_hud_key", "settings.preview_hud_key"));

            ShowAllHud = Toggle(cfg, "Show All HUD", "settings.name.show_all_hud", "settings.show_all_hud");
            ShowVitals = Toggle(cfg, "Show Vitals Panel", "settings.name.show_vitals_panel", "settings.show_vitals_panel");
            ShowHitMarker = Toggle(cfg, "Show Hit Marker", "settings.name.show_hit_marker", "settings.show_hit_marker");
            ShowDamageNumbers = Toggle(cfg, "Show Damage Numbers", "settings.name.show_damage_numbers", "settings.show_damage_numbers");
            ShowKillFeed = Toggle(cfg, "Show Kill Feed", "settings.name.show_kill_feed", "settings.show_kill_feed");
            ShowDamageDirection = Toggle(cfg, "Show Damage Direction", "settings.name.show_damage_direction", "settings.show_damage_direction");
            ShowSilhouette = Toggle(cfg, "Show Silhouette", "settings.name.show_silhouette", "settings.show_silhouette");
            ShowHealthLabel = Toggle(cfg, "Show Health Label", "settings.name.show_health_label", "settings.show_health_label");
            ShowHydrationLabel = Toggle(cfg, "Show Hydration Label", "settings.name.show_hydration_label", "settings.show_hydration_label");
            ShowEnergyLabel = Toggle(cfg, "Show Energy Label", "settings.name.show_energy_label", "settings.show_energy_label");
            ShowEffects = Toggle(cfg, "Show Effects", "settings.name.show_effects", "settings.show_effects");
            ShowEquipment = Toggle(cfg, "Show Equipment", "settings.name.show_equipment", "settings.show_equipment");
            ShowMovement = Toggle(cfg, "Show Movement", "settings.name.show_movement", "settings.show_movement");
            ShowStance = Toggle(cfg, "Show Stance", "settings.name.show_stance", "settings.show_stance");
            ShowSpeedIndicator = Toggle(cfg, "Show Volume Indicator", "settings.name.show_volume_indicator", "settings.show_volume_indicator");
            ShowWeaponName = Toggle(cfg, "Show Weapon Name", "settings.name.show_weapon_name", "settings.show_weapon_name");
            ShowAmmo = Toggle(cfg, "Show Ammo", "settings.name.show_ammo", "settings.show_ammo");
            ShowGrenades = Toggle(cfg, "Show Grenades", "settings.name.show_grenades", "settings.show_grenades");
            ShowStaminaBars = Toggle(cfg, "Show Stamina Bars", "settings.name.show_stamina_bars", "settings.show_stamina_bars");

            HitMarkerThickness = cfg.Bind(Main, "Hit Marker Thickness", 0f, new ConfigDescription(
                TextLocalization.Get("settings.hit_marker_thickness"),
                new AcceptableValueRange<float>(0f, 8f),
                DisplayName("settings.name.hit_marker_thickness")));
            Scale = cfg.Bind(Main, "HUD Scale", 1f, new ConfigDescription(
                TextLocalization.Get("settings.hud_scale"),
                new AcceptableValueRange<float>(0.5f, 2f),
                DisplayName("settings.name.hud_scale")));
            Opacity = cfg.Bind(Main, "HUD Opacity", 1f, new ConfigDescription(
                TextLocalization.Get("settings.hud_opacity"),
                new AcceptableValueRange<float>(0.2f, 1f),
                DisplayName("settings.name.hud_opacity")));

            // whole panels
            Pair(cfg, Layout, "Vitals", "layout.vitals_x", "layout.vitals_y", out VitalsX, out VitalsY);
            Pair(cfg, Layout, "Silhouette", "layout.silhouette_x", "layout.silhouette_y", out SilhouetteX, out SilhouetteY);
            Pair(cfg, Layout, "Health", "layout.health_x", "layout.health_y", out HealthX, out HealthY);
            Pair(cfg, Layout, "Hydration", "layout.hydration_x", "layout.hydration_y", out HydrationX, out HydrationY);
            Pair(cfg, Layout, "Energy", "layout.energy_x", "layout.energy_y", out EnergyX, out EnergyY);
            Pair(cfg, Layout, "Status", "layout.status_x", "layout.status_y", out StatusX, out StatusY);
            Pair(cfg, Layout, "Effects", "layout.effects_x", "layout.effects_y", out EffectsX, out EffectsY);
            Pair(cfg, Layout, "Equipment", "layout.equipment_x", "layout.equipment_y", out EquipmentX, out EquipmentY);
            Pair(cfg, Layout, "Movement", "layout.movement_x", "layout.movement_y", out MovementX, out MovementY);

            // single elements
            Pair(cfg, Elements, "Light Bleeding", "elements.light_bleeding_x", "elements.light_bleeding_y", out EffectsLightX, out EffectsLightY);
            Pair(cfg, Elements, "Heavy Bleeding", "elements.heavy_bleeding_x", "elements.heavy_bleeding_y", out EffectsHeavyX, out EffectsHeavyY);
            Pair(cfg, Elements, "Fracture", "elements.fracture_x", "elements.fracture_y", out EffectsFractureX, out EffectsFractureY);
            Pair(cfg, Elements, "Pain", "elements.pain_x", "elements.pain_y", out EffectsPainX, out EffectsPainY);
            Pair(cfg, Elements, "Weapon", "elements.weapon_x", "elements.weapon_y", out EquipmentWeaponX, out EquipmentWeaponY, -60);
            Pair(cfg, Elements, "Ammo", "elements.ammo_x", "elements.ammo_y", out EquipmentAmmoX, out EquipmentAmmoY, -60);
            Pair(cfg, Elements, "Grenades", "elements.grenades_x", "elements.grenades_y", out EquipmentGrenadesX, out EquipmentGrenadesY, -60);
            Pair(cfg, Elements, "Stance", "elements.stance_x", "elements.stance_y", out MovementStanceX, out MovementStanceY);
            Pair(cfg, Elements, "Movement Icon", "elements.movement_icon_x", "elements.movement_icon_y", out MovementSpeedX, out MovementSpeedY, 5);
            Pair(cfg, Elements, "Stamina", "elements.stamina_x", "elements.stamina_y", out MovementStaminaX, out MovementStaminaY, 10);
            Pair(cfg, Elements, "Hand Stamina", "elements.hand_stamina_x", "elements.hand_stamina_y", out MovementHandsX, out MovementHandsY, 10);
            Pair(cfg, Elements, "Leg Stamina Bar", "elements.leg_stamina_bar_x", "elements.leg_stamina_bar_y", out MovementStaminaBarX, out MovementStaminaBarY);
            Pair(cfg, Elements, "Hand Stamina Bar", "elements.hand_stamina_bar_x", "elements.hand_stamina_bar_y", out MovementHandsBarX, out MovementHandsBarY, 5);
            Pair(cfg, Elements, "Kill Feed", "elements.kill_feed_x", "elements.kill_feed_y", out KillFeedX, out KillFeedY);
            Pair(cfg, Elements, "Hit Marker", "elements.hit_marker_x", "elements.hit_marker_y", out HitMarkerX, out HitMarkerY);
            Pair(cfg, Elements, "Damage Numbers", "elements.damage_numbers_x", "elements.damage_numbers_y", out DamageNumbersX, out DamageNumbersY);
            Pair(cfg, Elements, "Damage Direction", "elements.damage_direction_x", "elements.damage_direction_y", out DamageDirectionX, out DamageDirectionY);
        }

        private static string[] GetThemeNames(string dir)
        {
            if (!Directory.Exists(dir)) return Array.Empty<string>();

            // a folder only counts as a theme if it has a theme.xml
            return Directory.GetDirectories(dir)
                .Where(d => Directory.GetFiles(d).Any(f =>
                    string.Equals(Path.GetFileName(f), "theme.xml", StringComparison.OrdinalIgnoreCase)))
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static ConfigEntry<bool> Toggle(ConfigFile cfg, string key, string nameKey, string descriptionKey)
        {
            return cfg.Bind(Main, key, true,
                ConfigDescription(nameKey, descriptionKey));
        }

        // X/Y offset pair, "label X" and "label Y" in the config
        private static void Pair(ConfigFile cfg, string category, string label, string xKey, string yKey,
            out ConfigEntry<int> x, out ConfigEntry<int> y, int defaultY = 0)
        {
            x = Offset(cfg, category, label + " X", "settings.name." + xKey, "settings." + xKey, 0);
            y = Offset(cfg, category, label + " Y", "settings.name." + yKey, "settings." + yKey, defaultY);
        }

        private static ConfigEntry<int> Offset(ConfigFile cfg, string category, string key,
            string nameKey, string descriptionKey, int defaultValue)
        {
            return cfg.Bind(category, key, defaultValue,
                ConfigDescription(nameKey, descriptionKey, new AcceptableValueRange<int>(-2000, 2000)));
        }

        private static ConfigDescription ConfigDescription(string nameKey, string descriptionKey,
            AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(TextLocalization.Get(descriptionKey), acceptableValues,
                DisplayName(nameKey));
        }

        private static ConfigurationManagerAttributes DisplayName(string nameKey)
        {
            return new ConfigurationManagerAttributes
            {
                DispName = TextLocalization.Get(nameKey)
            };
        }
    }
}