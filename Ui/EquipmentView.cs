using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HudBreakout
{
    internal class EquipmentView
    {
        private const int GrenadeScanInterval = 20; // frames, walking the inventory isn't free

        // indexes into Equipment.Slots, rig and pockets are where grenades live
        private const int RigSlot = 6;
        private const int PocketsSlot = 10;

        private enum WeaponKind
        {
            Knife,
            Unarmed,
            Shotgun,
            AssaultCarbine,
            Sniper,
            Smg,
            Machinegun,
            Pistol,
            AssaultRifle
        }

        private static readonly Dictionary<string, WeaponKind> KindByClass =
            new Dictionary<string, WeaponKind>(StringComparer.OrdinalIgnoreCase)
            {
                { "assaultCarbine", WeaponKind.AssaultCarbine },
                { "sniperRifle", WeaponKind.Sniper },
                { "smg", WeaponKind.Smg },
                { "machinegun", WeaponKind.Machinegun },
                { "pistol", WeaponKind.Pistol }
            };

        private static readonly string[] ShotgunCaliberStarts =
            { "12x70", "12x76", "20x70", "23x75", "410x", "1270", "1276", "2070" };
        private static readonly string[] ShotgunCaliberTags = { "12g", "20g" };
        private static readonly string[] ShotgunNameTags = { "shotgun", "12ga" };

        public RectTransform Root { get; }
        public float ThemeScale { get; }
        public bool ThemeShow { get; }

        private readonly Theme _theme;
        private readonly XNode _settings;
        private readonly Vector2 _position;
        private readonly bool _useCards;
        private readonly bool _rightAligned;
        private readonly string _grenadeLabel;

        private TMP_Text _weaponName, _ammo, _grenades;
        private Image _weaponIcon, _ammoIcon, _grenadeIcon;
        private Vector2 _weaponIconPosition, _weaponTextPosition;
        private Vector2 _ammoIconPosition, _ammoTextPosition;
        private Vector2 _grenadeIconPosition, _grenadeTextPosition;

        private Item _lastItem;
        private bool _hasLastItem;
        private string _lastWeaponKey, _localizedWeaponName;
        private string _lastWeaponIcon, _lastAmmoIcon;
        private int _lastAmmoCurrent = int.MinValue, _lastAmmoMax = int.MinValue;
        private Player _lastGrenadePlayer;
        private int _lastGrenadeScanFrame = -1;
        private int _lastGrenadeCount = -1;

        public EquipmentView(Transform parent, Theme theme)
        {
            _theme = theme;
            _settings = theme.Root.GetChild("Equipment");
            ThemeShow = _settings.GetBoolean("show", true);
            ThemeScale = _settings.GetFloat("scale", 1f);
            _grenadeLabel = _settings.GetString("grenadeLabel", "GRENADES");

            var anchor = _settings.GetAnchor("anchor", Vector2.one);
            _rightAligned = anchor.x > 0.75f;
            _useCards = theme.IconsEnabled && _settings.GetBoolean("cards", true);
            _position = new Vector2(_settings.GetFloat("x", -28), _settings.GetFloat("y", 34));
            Root = UiKit.CreateNode(parent, "Equipment", anchor, anchor, _position, Vector2.zero);

            var fontSize = _settings.GetInt("fontSize", 15);
            if (_useCards) BuildCards(fontSize);
            else BuildRows(fontSize);

            if (_weaponIcon) _weaponIcon.color = theme.IconColor("weapon", theme.Text);
            if (_ammoIcon) _ammoIcon.color = theme.IconColor("ammo", theme.Text);
            if (_grenadeIcon) _grenadeIcon.color = theme.IconColor("grenade", theme.Text);
        }

        // three side-by-side panels: weapon, ammo, grenades, laid out from the right edge
        private void BuildCards(int fontSize)
        {
            var weaponWidth = _settings.GetFloat("weaponPanelWidth", 126);
            var ammoWidth = _settings.GetFloat("ammoPanelWidth", 84);
            var grenadeWidth = _settings.GetFloat("grenadePanelWidth", 48);
            var panelHeight = _settings.GetFloat("panelHeight", 46);
            var panelGap = _settings.GetFloat("panelGap", 2);
            var weaponIconSize = _settings.GetFloat("weaponIconSize", 30);
            var ammoIconSize = _settings.GetFloat("ammoIconSize", 14);
            var grenadeIconSize = _settings.GetFloat("grenadeIconSize", 18);

            var grenadeOffset = 0f;
            var ammoOffset = grenadeWidth + panelGap;
            var weaponOffset = ammoOffset + ammoWidth + panelGap;

            _weaponIconPosition = new Vector2(-weaponOffset - 8, (panelHeight - weaponIconSize) * 0.5f);
            _ammoIconPosition = new Vector2(-ammoOffset - 6, (panelHeight - ammoIconSize) * 0.5f);
            _grenadeIconPosition = new Vector2(-grenadeOffset - 6, (panelHeight - grenadeIconSize) * 0.5f);
            _weaponTextPosition = new Vector2(-weaponOffset - weaponWidth + 8, 0);
            _ammoTextPosition = new Vector2(-ammoOffset - ammoWidth + 4, 0);
            _grenadeTextPosition = new Vector2(-grenadeOffset - grenadeWidth + 4, 0);

            _weaponIcon = Icon("weaponIcon", "weapon.png", _weaponIconPosition, weaponIconSize);
            _weaponName = CardLabel("WeaponName", fontSize, _weaponTextPosition,
                new Vector2(weaponWidth - weaponIconSize - 20, panelHeight),
                CardAlignment("weaponTextAlign", TextAnchor.MiddleLeft));

            _ammoIcon = Icon("ammoIcon", "ammo.png", _ammoIconPosition, ammoIconSize);
            _ammo = CardLabel("Ammo", fontSize, _ammoTextPosition,
                new Vector2(ammoWidth - ammoIconSize - 18, panelHeight),
                CardAlignment("ammoTextAlign", TextAnchor.MiddleCenter));

            _grenadeIcon = Icon("grenadeIcon", "grenade.png", _grenadeIconPosition, grenadeIconSize);
            _grenades = CardLabel("Grenades", fontSize, _grenadeTextPosition,
                new Vector2(grenadeWidth - grenadeIconSize - 18, panelHeight),
                CardAlignment("grenadeTextAlign", TextAnchor.MiddleCenter));
        }

        // plain stacked rows: weapon, ammo, grenades
        private void BuildRows(int fontSize)
        {
            var iconSize = _settings.GetFloat("iconSize", 36);
            var rowHeight = _settings.GetFloat("rowHeight", 36);
            var textX = _theme.IconsEnabled ? iconSize + 10 : 0;
            var textOffset = _rightAligned ? -textX : textX;

            _weaponIconPosition = new Vector2(0, 4);
            _weaponTextPosition = new Vector2(textOffset, 4);
            _ammoIconPosition = new Vector2(0, -rowHeight);
            _ammoTextPosition = new Vector2(textOffset, -rowHeight);
            _grenadeIconPosition = new Vector2(0, -rowHeight * 2);
            _grenadeTextPosition = new Vector2(textOffset, -rowHeight * 2);

            _weaponIcon = Icon("weaponIcon", "weapon.png", _weaponIconPosition,
                _settings.GetFloat("weaponIconSize", iconSize));
            _weaponName = RowLabel("WeaponName", fontSize, _weaponTextPosition, new Vector2(250, rowHeight));
            _ammoIcon = Icon("ammoIcon", "ammo.png", _ammoIconPosition,
                _settings.GetFloat("ammoIconSize", iconSize));
            _ammo = RowLabel("Ammo", fontSize, _ammoTextPosition, new Vector2(180, rowHeight));
            _grenadeIcon = Icon("grenadeIcon", "grenade.png", _grenadeIconPosition,
                _settings.GetFloat("grenadeIconSize", iconSize));
            _grenades = RowLabel("Grenades", fontSize, _grenadeTextPosition, new Vector2(180, rowHeight));
        }

        private Image Icon(string key, string fallback, Vector2 position, float size)
        {
            if (!_theme.IconsEnabled) return null;

            var file = _settings.GetString(key, fallback);
            var sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
            if (!sprite) return null;

            var image = UiKit.CreateImage(Root, key, sprite, _theme.Text, Vector2.zero,
                _rightAligned ? Vector2.right : Vector2.zero, position, new Vector2(size, size));
            image.preserveAspect = true;
            return image;
        }

        private TMP_Text CardLabel(string name, int fontSize, Vector2 position, Vector2 box, TextAnchor alignment)
        {
            return UiKit.CreateLabel(Root, name, "", fontSize, _theme.Text, alignment,
                Vector2.right, Vector2.zero, position, box);
        }

        private TMP_Text RowLabel(string name, int fontSize, Vector2 position, Vector2 box)
        {
            return UiKit.CreateLabel(Root, name, "", fontSize, _theme.Text,
                _rightAligned ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Vector2.zero,
                _rightAligned ? Vector2.right : Vector2.zero, position, box);
        }

        public void Update(Player player)
        {
            PlaceRows();
            ApplyFeatureToggles();

            var item = player.HandsController?.Item;
            var weapon = item as Weapon;

            RefreshWeaponName(item);
            RefreshIcons(item, weapon);
            RefreshAmmo(weapon);
            RefreshGrenades(player);
        }

        public void Preview()
        {
            PlaceRows();
            _weaponName.text = "MP-133";
            _ammo.text = "8/8";
            _grenades.text = GrenadeText(2);

            SetIcon(_weaponIcon, ref _lastWeaponIcon, _settings.GetString("assaultRifleIcon", "weapon_assault_rifle.png"));
            SetIcon(_ammoIcon, ref _lastAmmoIcon, _settings.GetString("mediumAmmoIcon", "ammo_medium.png"));
            if (_weaponIcon) _weaponIcon.color = _theme.IconColor("assaultRifle", _theme.Good);
            if (_ammoIcon) _ammoIcon.color = _theme.IconColor("mediumAmmo", _theme.Warn);
            ApplyFeatureToggles();

            // force the next real Update to redraw everything the preview overwrote
            _hasLastItem = false;
            _lastAmmoCurrent = int.MinValue;
            _lastAmmoMax = int.MinValue;
            _lastGrenadeScanFrame = -1;
            _lastGrenadeCount = -1;
        }

        public void Reset()
        {
            // let go of the old raid's inventory and refresh everything on the next one
            _lastItem = null;
            _hasLastItem = false;
            _lastGrenadePlayer = null;
            _lastGrenadeScanFrame = -1;
            _lastGrenadeCount = -1;
            _lastAmmoCurrent = int.MinValue;
            _lastAmmoMax = int.MinValue;
        }

        private void RefreshWeaponName(Item item)
        {
            var key = item?.ShortName;
            if (key != _lastWeaponKey || _localizedWeaponName == null)
            {
                _lastWeaponKey = key;
                _localizedWeaponName = (key != null ? HudLocalization.Localized(key) : "UNARMED").ToUpperInvariant();
            }

            if (_weaponName.text != _localizedWeaponName) _weaponName.text = _localizedWeaponName;
        }

        // the item only changes when the player swaps weapons, so don't classify it every frame
        private void RefreshIcons(Item item, Weapon weapon)
        {
            if (_hasLastItem && ReferenceEquals(item, _lastItem)) return;

            _lastItem = item;
            _hasLastItem = true;

            var kind = ClassifyWeapon(item, weapon);
            var ammoIcon = AmmoIcon(kind);
            SetIcon(_weaponIcon, ref _lastWeaponIcon, WeaponIcon(kind, weapon));
            SetIcon(_ammoIcon, ref _lastAmmoIcon, ammoIcon);
            if (_weaponIcon) _weaponIcon.color = _theme.IconColor(WeaponColorKey(kind), _theme.Text);
            if (_ammoIcon) _ammoIcon.color = _theme.IconColor(AmmoColorKey(ammoIcon), _theme.Text);
        }

        private void RefreshAmmo(Weapon weapon)
        {
            var current = weapon != null ? weapon.GetCurrentMagazineCount() : -1;
            var max = weapon != null ? weapon.GetMaxMagazineCount() : -1;
            if (current == _lastAmmoCurrent && max == _lastAmmoMax) return;

            _lastAmmoCurrent = current;
            _lastAmmoMax = max;
            var text = weapon != null ? current + "/" + max : "--/--";
            if (_ammo.text != text) _ammo.text = text;
        }

        private void RefreshGrenades(Player player)
        {
            var due = _lastGrenadePlayer != player || _lastGrenadeScanFrame < 0 ||
                      Time.frameCount - _lastGrenadeScanFrame >= GrenadeScanInterval;
            if (!due) return;

            _lastGrenadePlayer = player;
            _lastGrenadeScanFrame = Time.frameCount;

            var count = CountGrenades(player);
            if (count == _lastGrenadeCount) return;

            _lastGrenadeCount = count;
            var text = GrenadeText(count);
            if (_grenades.text != text) _grenades.text = text;
        }

        private string GrenadeText(int count)
        {
            return _useCards ? count.ToString() : _grenadeLabel + "  " + count;
        }

        // root plus the three rows, each with the player's config offsets on top
        private void PlaceRows()
        {
            var root = _position + new Vector2(Settings.EquipmentX.Value, Settings.EquipmentY.Value);
            if (Root.anchoredPosition != root) Root.anchoredPosition = root;

            SetRowPosition(_weaponIcon, _weaponName, _weaponIconPosition, _weaponTextPosition,
                Settings.EquipmentWeaponX.Value, Settings.EquipmentWeaponY.Value);
            SetRowPosition(_ammoIcon, _ammo, _ammoIconPosition, _ammoTextPosition,
                Settings.EquipmentAmmoX.Value, Settings.EquipmentAmmoY.Value);
            SetRowPosition(_grenadeIcon, _grenades, _grenadeIconPosition, _grenadeTextPosition,
                Settings.EquipmentGrenadesX.Value, Settings.EquipmentGrenadesY.Value);
        }

        private static void SetRowPosition(Image icon, TMP_Text text, Vector2 iconPosition, Vector2 textPosition,
            float offsetX, float offsetY)
        {
            var offset = new Vector2(offsetX, offsetY);
            if (icon) SetPosition(icon.rectTransform, iconPosition + offset);
            if (text) SetPosition(text.rectTransform, textPosition + offset);
        }

        private static void SetPosition(RectTransform rectTransform, Vector2 position)
        {
            if (rectTransform.anchoredPosition != position) rectTransform.anchoredPosition = position;
        }

        private void ApplyFeatureToggles()
        {
            SetActive(_weaponIcon, Settings.ShowWeaponName.Value);
            SetActive(_weaponName, Settings.ShowWeaponName.Value);
            SetActive(_ammoIcon, Settings.ShowAmmo.Value);
            SetActive(_ammo, Settings.ShowAmmo.Value);
            SetActive(_grenadeIcon, Settings.ShowGrenades.Value);
            SetActive(_grenades, Settings.ShowGrenades.Value);
        }

        private TextAnchor CardAlignment(string key, TextAnchor fallback)
        {
            switch (_settings.GetString(key, "").ToLowerInvariant())
            {
                case "left": return TextAnchor.MiddleLeft;
                case "center": return TextAnchor.MiddleCenter;
                case "right": return TextAnchor.MiddleRight;
                default: return fallback; // missing or unrecognized: keep the current look
            }
        }

        private static void SetActive(Graphic graphic, bool active)
        {
            if (graphic && graphic.gameObject.activeSelf != active) graphic.gameObject.SetActive(active);
        }

        private void SetIcon(Image image, ref string lastFile, string file)
        {
            if (!image || file == lastFile) return;

            lastFile = file;
            image.sprite = _theme.LoadIconSprite(Path.Combine("Icons", file));
        }

        private static WeaponKind ClassifyWeapon(Item item, Weapon weapon)
        {
            if (item != null && item.GetItemComponent<KnifeComponent>() != null) return WeaponKind.Knife;
            if (weapon == null) return WeaponKind.Unarmed;

            var weaponClass = weapon.WeapClass ?? "";
            if (IsShotgun(weapon, weaponClass, item.ShortName)) return WeaponKind.Shotgun;
            return KindByClass.TryGetValue(weaponClass, out var kind) ? kind : WeaponKind.AssaultRifle;
        }

        // class names aren't reliable for shotguns, so also look at the caliber and the name
        private static bool IsShotgun(Weapon weapon, string weaponClass, string shortName)
        {
            if (Contains(weaponClass, "shotgun")) return true;

            var caliber = (weapon.AmmoCaliber ?? "").Replace(".", "").Replace("/", "")
                .Replace("-", "").Replace(" ", "");
            return ShotgunCaliberStarts.Any(start => caliber.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                   || ShotgunCaliberTags.Any(tag => Contains(caliber, tag))
                   || ShotgunNameTags.Any(tag => Contains(shortName ?? "", tag));
        }

        private static bool Contains(string text, string part)
        {
            return text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string WeaponIcon(WeaponKind kind, Weapon weapon)
        {
            switch (kind)
            {
                case WeaponKind.Knife: return _settings.GetString("knifeIcon", "weapon_knife.png");
                case WeaponKind.Unarmed: return _settings.GetString("weaponIcon", "weapon.png");
                case WeaponKind.Shotgun: return _settings.GetString("shotgunIcon", "weapon_shotgun.png");
                case WeaponKind.AssaultCarbine: return _settings.GetString("assaultCarbineIcon", "weapon_assault_carbine.png");
                case WeaponKind.Sniper: return _settings.GetString("sniperIcon", "weapon_sniper.png");
                case WeaponKind.Smg: return _settings.GetString("smgIcon", "weapon_smg.png");
                case WeaponKind.Machinegun: return _settings.GetString("lmgIcon", "weapon_lmg.png");
                case WeaponKind.Pistol: return _settings.GetString("pistolIcon", "weapon_pistol.png");
                default:
                    // a weapon whose class says "knife" still gets the knife icon
                    if (weapon != null && string.Equals(weapon.WeapClass, "knife", StringComparison.OrdinalIgnoreCase))
                        return _settings.GetString("knifeIcon", "weapon_knife.png");
                    return _settings.GetString("assaultRifleIcon", "weapon_assault_rifle.png");
            }
        }

        private static string WeaponColorKey(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Knife: return "knife";
                case WeaponKind.Unarmed: return "weapon";
                case WeaponKind.Shotgun: return "shotgun";
                case WeaponKind.AssaultCarbine: return "assaultCarbine";
                case WeaponKind.Sniper: return "sniper";
                case WeaponKind.Smg: return "smg";
                case WeaponKind.Machinegun: return "lmg";
                case WeaponKind.Pistol: return "pistol";
                default: return "assaultRifle";
            }
        }

        // small for smg/pistol, large for sniper/lmg, medium for everything else
        private string AmmoIcon(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Smg:
                case WeaponKind.Pistol:
                    return _settings.GetString("smallAmmoIcon", "ammo_small.png");
                case WeaponKind.Sniper:
                case WeaponKind.Machinegun:
                    return _settings.GetString("largeAmmoIcon", "ammo_large.png");
                default:
                    return _settings.GetString("mediumAmmoIcon", "ammo_medium.png");
            }
        }

        // the color follows the size, which we read back from the (possibly themed) file name
        private static string AmmoColorKey(string ammoIconFile)
        {
            if (Contains(ammoIconFile, "large")) return "largeAmmo";
            if (Contains(ammoIconFile, "medium")) return "mediumAmmo";
            return "smallAmmo";
        }

        private static int CountGrenades(Player player)
        {
            var slots = player.Inventory?.Equipment?.Slots;
            if (slots == null) return 0;

            // only rig and pockets, the grenade quick slots point at the same items
            var count = slots.Length > RigSlot ? CountGrenadeItems(slots[RigSlot]?.ContainedItem) : 0;
            if (slots.Length > PocketsSlot) count += CountGrenadeItems(slots[PocketsSlot]?.ContainedItem);
            return count;
        }

        private static int CountGrenadeItems(Item item)
        {
            var count = item is ThrowWeap ? Math.Max(1, item.StackObjectsCount) : 0;
            var compound = item as CompoundItem;
            if (compound == null) return count;

            foreach (var grid in compound.Grids)
                foreach (var contained in grid.Items)
                    count += CountGrenadeItems(contained);

            foreach (var slot in compound.Slots)
                if (slot?.ContainedItem != null)
                    count += CountGrenadeItems(slot.ContainedItem);
            return count;
        }
    }
}