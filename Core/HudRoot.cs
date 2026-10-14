using Comfort.Common;
using EFT;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal class HudRoot : MonoBehaviour
    {
        public static Player MainPlayer { get; private set; }
        public static RectTransform Canvas { get; private set; }
        public static bool IsInventoryOpen { get; private set; }

        private CanvasGroup _group;
        private RectTransform _content;
        private Theme _theme;
        private VitalsView _vitals;
        private EffectsView _effects;
        private EquipmentView _equipment;
        private MovementView _movement;
        private CombatView _combat;
        private KillFeedView _killFeed;

        private string _builtTheme;
        private string _builtLanguage;
        private string _profileId;
        private float _previewUntil;
        private bool _previewDone;
        private bool _wasInRaid;
        private bool _escapeMenu;
        private bool _cursorSeen;

        internal static void SetInventoryOpen(bool open) => IsInventoryOpen = open;

        private void Awake()
        {
            try
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                _group = gameObject.AddComponent<CanvasGroup>();
                _group.blocksRaycasts = false;
                _group.interactable = false;
                Canvas = (RectTransform)transform;

                Rebuild();
                HudBreakoutPlugin.Log.LogInfo("HUD ready");
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("HUD build failed: " + ex);
                enabled = false;
            }
        }

        private void Rebuild()
        {
            _builtTheme = Settings.ThemeName.Value;
            _builtLanguage = Settings.Language.Value;
            Unsubscribe();
            _previewDone = false;

            if (_content)
            {
                // Destroy() is deferred, so hide the old one now or it flashes over the new HUD
                _content.gameObject.SetActive(false);
                Destroy(_content.gameObject);
            }

            _theme?.Dispose();
            _theme = Theme.Load(HudBreakoutPlugin.ThemesDir, _builtTheme);
            UiKit.CurrentTheme = _theme;

            _content = UiKit.CreateNode(transform, "Content", Vector2.zero, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            UiKit.StretchToFillParent(_content);

            _vitals = new VitalsView(_content, _theme);
            _effects = new EffectsView(_content, _theme);
            _equipment = new EquipmentView(_content, _theme);
            _movement = new MovementView(_content, _theme);
            _combat = new CombatView(_content, _theme);
            _killFeed = new KillFeedView(_content, _theme);

            HudEvents.DamageTaken += OnDamageTaken;
            HudEvents.HitDealt += _combat.OnHit;
            HudEvents.Kill += _killFeed.OnKill;

            _profileId = null;
            HudBreakoutPlugin.Log.LogInfo("theme: " + _builtTheme);
        }

        private void Unsubscribe()
        {
            HudEvents.DamageTaken -= OnDamageTaken;
            if (_combat != null) HudEvents.HitDealt -= _combat.OnHit;
            if (_killFeed != null) HudEvents.Kill -= _killFeed.OnKill;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _theme?.Dispose();
            IsInventoryOpen = false;
            if (Canvas == transform)
            {
                MainPlayer = null;
                Canvas = null;
            }
        }

        private void OnDamageTaken(DamageTakenEvent e)
        {
            _vitals.OnDamage(e);
            _combat.OnDamageTaken(e);
        }

        public static Vector2 ScreenToLocal(Vector2 screenPos)
        {
            if (!Canvas) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Canvas, screenPos, null, out var local);
            return local;
        }

        private void Update()
        {
            if (Settings.Language.Value != _builtLanguage)
            {
                TextLocalization.SetLanguage(Settings.Language.Value);
                Rebuild();
            }

            if (Settings.ReloadKey.Value.IsDown() || Settings.ThemeName.Value != _builtTheme)
                Rebuild();

            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            MainPlayer = FindMainPlayer(world);
            var inRaid = IsInRaid(world, MainPlayer);

            if (inRaid && Settings.PreviewKey.Value.IsDown())
            {
                _previewUntil = Time.unscaledTime + 5f;
                _previewDone = false;
            }

            TrackEscapeMenu();

            var preview = inRaid && Time.unscaledTime < _previewUntil;
            var alpha = 0f;
            if (inRaid && Settings.ShowAllHud.Value && !_escapeMenu)
                alpha = IsInventoryOpen ? 0.2f : Settings.Opacity.Value;
            if (!Mathf.Approximately(_group.alpha, alpha)) _group.alpha = alpha;

            if (!inRaid)
            {
                IsInventoryOpen = false;
                _escapeMenu = false;
                _cursorSeen = false;
                if (_wasInRaid)
                {
                    ResetAll();
                    _profileId = null;
                }
                _wasInRaid = false;
                return;
            }

            // profile ids can repeat between raids, so always reset on entry
            if (!_wasInRaid || _profileId != MainPlayer.ProfileId)
            {
                _profileId = MainPlayer.ProfileId;
                ResetAll();
            }
            _wasInRaid = true;

            var scale = Settings.Scale.Value;
            Place(_vitals.Root, _vitals.ThemeScale * scale, _vitals.ThemeShow, Settings.ShowVitals.Value, preview);
            Place(_effects.Root, _effects.ThemeScale * scale, _effects.ThemeShow, Settings.ShowEffects.Value, preview);
            Place(_equipment.Root, _equipment.ThemeScale * scale, _equipment.ThemeShow, Settings.ShowEquipment.Value, preview);
            Place(_movement.Root, _movement.ThemeScale * scale, _movement.ThemeShow, Settings.ShowMovement.Value, preview);
            Place(_killFeed.Root, _killFeed.ThemeScale * scale, _killFeed.ThemeShow, Settings.ShowKillFeed.Value, preview);
            SetScale(_combat.Root, scale);
            SetActive(_combat.Root.gameObject, true);

            var dt = Time.unscaledDeltaTime;
            if (preview)
            {
                if (!_previewDone)
                {
                    _vitals.Preview();
                    _effects.Preview();
                    _equipment.Preview();
                    _movement.Preview();
                    _combat.Preview();
                    _killFeed.Preview();
                    _previewDone = true;
                }
            }
            else
            {
                if (_vitals.Root.gameObject.activeSelf) _vitals.Update(MainPlayer, dt);
                if (_effects.Root.gameObject.activeSelf) _effects.Update(MainPlayer);
                if (_equipment.Root.gameObject.activeSelf) _equipment.Update(MainPlayer);
                if (_movement.Root.gameObject.activeSelf) _movement.Update(MainPlayer, dt);
            }

            _combat.Update(dt);
            _killFeed.Update(dt);
        }

        // The pause menu has no event we can hook, so watch the cursor:
        // Esc with no cursor means it opened, cursor going away again means it closed
        private void TrackEscapeMenu()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !IsInventoryOpen && !Cursor.visible)
                _escapeMenu = true;
            if (!_escapeMenu) return;

            if (Cursor.visible) _cursorSeen = true;
            else if (_cursorSeen) _escapeMenu = _cursorSeen = false;
        }

        private static Player FindMainPlayer(GameWorld world)
        {
            if (world == null) return null;
            if (world.MainPlayer != null) return world.MainPlayer;

            var alive = world.AllAlivePlayersList;
            if (alive == null) return null;
            foreach (var p in alive)
                if (p != null && p.IsYourPlayer) return p;
            return null;
        }

        private void ResetAll()
        {
            _vitals.Reset();
            _combat.Reset();
            _equipment.Reset();
            _killFeed.Reset();
        }

        private static void Place(RectTransform root, float scale, bool themeShow, bool settingOn, bool preview)
        {
            SetScale(root, scale);
            SetActive(root.gameObject, preview || (themeShow && settingOn));
        }

        private static void SetScale(RectTransform rt, float scale)
        {
            var s = Vector3.one * scale;
            if (rt.localScale != s) rt.localScale = s;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }

        private static bool IsInRaid(GameWorld world, Player p)
        {
            if (world == null || p == null || !p.IsYourPlayer || p.HealthController == null)
                return false;
            if (string.Equals(world.LocationId, "hideout", StringComparison.OrdinalIgnoreCase))
                return false;

            // player and world exist during the loading screen, so wait for the game to really start
            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            return game != null && game.Status == GameStatus.Started;
        }
    }
}