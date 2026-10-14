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
        public static ConfigEntry<string> ThemeName;
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

        public static void Bind(ConfigFile cfg, string themesDir)
        {
            var themes = GetThemeNames(themesDir);
            if (themes.Length == 0) themes = new[] { "Minimal", "TextOnly" };

            ThemeName = cfg.Bind(Main, "Theme", "Minimal",
                new ConfigDescription("HUD theme folder. Changes apply instantly.",
                    new AcceptableValueList<string>(themes)));
            ReloadKey = cfg.Bind(Main, "Reload Theme Key", new KeyboardShortcut(KeyCode.F9),
                "Reloads theme.xml without restarting the game.");
            PreviewKey = cfg.Bind(Main, "Preview HUD Key", new KeyboardShortcut(KeyCode.F10),
                "Shows a sample of all HUD panels for five seconds.");

            ShowAllHud = Toggle(cfg, "Show All HUD", "Master toggle for every HudBreakout panel.");
            ShowVitals = Toggle(cfg, "Show Vitals Panel", "Body silhouette, health, energy and hydration.");
            ShowHitMarker = Toggle(cfg, "Show Hit Marker", "Marker when you hit an enemy.");
            ShowDamageNumbers = Toggle(cfg, "Show Damage Numbers", "Floating damage numbers at the impact point.");
            ShowKillFeed = Toggle(cfg, "Show Kill Feed", "Kill lines with weapon, distance and body part.");
            ShowDamageDirection = Toggle(cfg, "Show Damage Direction", "Indicators pointing to where damage came from.");
            ShowSilhouette = Toggle(cfg, "Show Silhouette", "Show or hide the seven-part health silhouette.");
            ShowHealthLabel = Toggle(cfg, "Show Health Label", "Show or hide the Health label.");
            ShowHydrationLabel = Toggle(cfg, "Show Hydration Label", "Show or hide the Hydration label.");
            ShowEnergyLabel = Toggle(cfg, "Show Energy Label", "Show or hide the Energy label.");
            ShowEffects = Toggle(cfg, "Show Effects", "Show active status effects such as bleeding, pain, fatigue and buffs.");
            ShowEquipment = Toggle(cfg, "Show Equipment", "Show weapon, ammunition and grenade count.");
            ShowMovement = Toggle(cfg, "Show Movement", "Show stance, movement speed and stamina.");
            ShowStance = Toggle(cfg, "Show Stance", "Show the stance icon and any enabled stance text.");
            ShowSpeedIndicator = Toggle(cfg, "Show Volume Indicator", "Show the movement volume/sprint-speed icon.");
            ShowWeaponName = Toggle(cfg, "Show Weapon Name", "Show the equipped weapon icon and name.");
            ShowAmmo = Toggle(cfg, "Show Ammo", "Show the ammunition icon and magazine count.");
            ShowGrenades = Toggle(cfg, "Show Grenades", "Show the grenade icon and count.");
            ShowStaminaBars = Toggle(cfg, "Show Stamina Bars", "Show both leg/body and hand stamina bars.");

            HitMarkerThickness = cfg.Bind(Main, "Hit Marker Thickness", 0f, new ConfigDescription(
                "Line thickness in pixels. 0 uses the theme value.", new AcceptableValueRange<float>(0f, 8f)));
            Scale = cfg.Bind(Main, "HUD Scale", 1f, new ConfigDescription(
                "Overall HUD scale.", new AcceptableValueRange<float>(0.5f, 2f)));
            Opacity = cfg.Bind(Main, "HUD Opacity", 1f, new ConfigDescription(
                "Opacity multiplier for the whole HUD.", new AcceptableValueRange<float>(0.2f, 1f)));

            // whole panels
            Pair(cfg, Layout, "Vitals", "the entire vitals HUD", out VitalsX, out VitalsY);
            Pair(cfg, Layout, "Silhouette", "the silhouette (relative to the vitals panel)", out SilhouetteX, out SilhouetteY);
            Pair(cfg, Layout, "Health", "the Health value and label", out HealthX, out HealthY);
            Pair(cfg, Layout, "Hydration", "the Hydration value and label", out HydrationX, out HydrationY);
            Pair(cfg, Layout, "Energy", "the Energy value and label", out EnergyX, out EnergyY);
            Pair(cfg, Layout, "Status", "the body-part status text", out StatusX, out StatusY);
            Pair(cfg, Layout, "Effects", "the status effects display", out EffectsX, out EffectsY);
            Pair(cfg, Layout, "Equipment", "the weapon and grenade display", out EquipmentX, out EquipmentY);
            Pair(cfg, Layout, "Movement", "the stance, speed and stamina display", out MovementX, out MovementY);

            // single elements
            Pair(cfg, Elements, "Light Bleeding", "the light-bleeding row", out EffectsLightX, out EffectsLightY);
            Pair(cfg, Elements, "Heavy Bleeding", "the heavy-bleeding row", out EffectsHeavyX, out EffectsHeavyY);
            Pair(cfg, Elements, "Fracture", "the fracture row", out EffectsFractureX, out EffectsFractureY);
            Pair(cfg, Elements, "Pain", "the pain row", out EffectsPainX, out EffectsPainY);
            Pair(cfg, Elements, "Weapon", "the weapon row", out EquipmentWeaponX, out EquipmentWeaponY, -60);
            Pair(cfg, Elements, "Ammo", "the ammo row", out EquipmentAmmoX, out EquipmentAmmoY, -60);
            Pair(cfg, Elements, "Grenades", "the grenade row", out EquipmentGrenadesX, out EquipmentGrenadesY, -60);
            Pair(cfg, Elements, "Stance", "the stance indicator", out MovementStanceX, out MovementStanceY);
            Pair(cfg, Elements, "Movement Icon", "the volume/sprint indicator", out MovementSpeedX, out MovementSpeedY, 5);
            Pair(cfg, Elements, "Stamina", "the body stamina display", out MovementStaminaX, out MovementStaminaY, 10);
            Pair(cfg, Elements, "Hand Stamina", "the hand stamina display", out MovementHandsX, out MovementHandsY, 10);
            Pair(cfg, Elements, "Leg Stamina Bar", "the leg/body stamina bar", out MovementStaminaBarX, out MovementStaminaBarY);
            Pair(cfg, Elements, "Hand Stamina Bar", "the hand stamina bar", out MovementHandsBarX, out MovementHandsBarY, 5);
            Pair(cfg, Elements, "Kill Feed", "the kill feed", out KillFeedX, out KillFeedY);
            Pair(cfg, Elements, "Hit Marker", "the hit marker", out HitMarkerX, out HitMarkerY);
            Pair(cfg, Elements, "Damage Numbers", "the damage numbers", out DamageNumbersX, out DamageNumbersY);
            Pair(cfg, Elements, "Damage Direction", "the damage indicator", out DamageDirectionX, out DamageDirectionY);
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

        private static ConfigEntry<bool> Toggle(ConfigFile cfg, string key, string description)
        {
            return cfg.Bind(Main, key, true, description);
        }

        // X/Y offset pair, "label X" and "label Y" in the config
        private static void Pair(ConfigFile cfg, string category, string label, string what,
            out ConfigEntry<int> x, out ConfigEntry<int> y, int defaultY = 0)
        {
            x = Offset(cfg, category, label + " X", "Move " + what + " horizontally.", 0);
            y = Offset(cfg, category, label + " Y", "Move " + what + " vertically.", defaultY);
        }

        private static ConfigEntry<int> Offset(ConfigFile cfg, string category, string key, string description,
            int defaultValue)
        {
            return cfg.Bind(category, key, defaultValue, new ConfigDescription(
                description + " Whole-pixel offset from the theme's XML position, in HUD reference pixels.",
                new AcceptableValueRange<int>(-2000, 2000)));
        }
    }
}