using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using UnityEngine;

namespace HudBreakout
{
    [BepInPlugin("com.deadwolf.hudbreakout", "HudBreakout", PluginInfo.Version)]
    public class HudBreakoutPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string ThemesDir;
        internal static string LocalizationDir;

        private GameObject _hudRoot;
        private Harmony _harmony;
        private float _nextHudRootCheckTime;

        private void Update()
        {
            if (Time.unscaledTime < _nextHudRootCheckTime) return;
            _nextHudRootCheckTime = Time.unscaledTime + 2f;

            if (_hudRoot != null && _hudRoot.activeInHierarchy && _hudRoot.GetComponent<HudRoot>() != null) return;

            Log.LogWarning("HUD object missing/inactive, recreating");
            if (_hudRoot != null) Destroy(_hudRoot);
            CreateHudRoot();
        }

        private void Awake()
        {
            Log = Logger;
            var pluginDirectory = Path.GetDirectoryName(Info.Location);
            ThemesDir = Path.Combine(pluginDirectory, "Themes");
            LocalizationDir = Path.Combine(pluginDirectory, "Localization");
            Config.SaveOnConfigSet = true;
            FontHelper.Load();
            Settings.Bind(Config, ThemesDir, LocalizationDir);
            _harmony = new Harmony("com.deadwolf.hudbreakout");
            _harmony.PatchAll(typeof(HudBreakoutPlugin).Assembly);
            CreateHudRoot();
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_hudRoot != null) Destroy(_hudRoot);
            UiKit.ReleaseOwnedResources();
        }

        private void CreateHudRoot()
        {
            _hudRoot = new GameObject("HudBreakout") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(_hudRoot);
            _hudRoot.AddComponent<HudRoot>();
        }
    }
}
