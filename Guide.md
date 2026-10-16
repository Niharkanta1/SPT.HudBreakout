# HudBreakout Customization Guide

This guide describes the files and settings used to customize HudBreakout's HUD, artwork, hit sounds, and language.

## Where the files live

After installation, the plugin files are under:

```text
BepInEx/plugins/HudBreakout/
├── Themes/
│   ├── Minimal/
│   │   ├── theme.xml
│   │   ├── Icons/
│   │   └── *.png
│   └── ...
├── Localization/
│   └── en.xml
└── Audio/
    ├── bullet-hit.wav
    ├── bullet-headshot.wav
    └── bullet-kill.wav
```

The source repository has the same `Themes`, `Localization`, and `Audio` folders at its root. Make a backup before changing shipped files. For a theme, copy an existing theme folder, give the copy a new folder name, edit its `theme.xml`, and select that folder in the BepInEx configuration. The folder is selectable when it contains a `theme.xml` file.

The BepInEx settings are stored in `BepInEx/config/com.deadwolf.hudbreakout.cfg`. Choose **F12** in game to open the configuration manager (if installed).

## Theme basics

Themes are XML files. The shipped `Themes/Minimal/theme.xml` is a complete example; `Colorful` and `TextOnly` show alternative styles. Attribute names are case-sensitive. Missing or invalid XML attributes generally use built-in defaults.

```xml
<Theme name="MyTheme" shadow="true">
  <Colors text="#E6E6E6" good="#55CC66" warn="#E5B84B"
          bad="#D94A42" dim="#929292" highlight="#F5D35E"
          lost="#3A3030" default="#FFFFFF" />
  <Vitals show="true" anchor="TopLeft" x="20" y="-20" scale="1">
    ...
  </Vitals>
</Theme>
```

- Colors accept `#RRGGBB` or `#RRGGBBAA` (the final two digits are alpha/opacity).
- Coordinates are HUD pixels on a 1920x1080 reference canvas. Positive `x` moves right; positive `y` moves up, relative to the element's anchor.
- Valid anchors: `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `Center`, `MiddleCenter`, `MiddleRight`, `BottomLeft`, `BottomCenter`, and `BottomRight`.
- `scale` on a panel scales that panel. The global **HUD Scale** setting multiplies the theme scales.
- Set `shadow="false"` on `<Theme>` to turn off text shadows. Text uses the game's Bender Normal TextMeshPro font; the theme does not select another font.
- XML booleans use `true` or `false`; numbers use a period for decimal values.

### Reload and preview

The **Reload Theme Key** defaults to **F9**. Use it in a raid after changing the selected theme's XML or PNG files; it rebuilds the HUD using the current theme. **Preview HUD Key** defaults to **F10** and displays sample HUD data for five seconds in a raid. F10 also plays a random loaded hit sound when hit sounds are enabled.

## HUD settings and positioning

The F12 configuration offers global visibility and positioning controls. These settings are independent of a theme's base XML positions:

- **Show All HUD** is the master visibility toggle. **Show Vitals Panel**, **Show Effects**, **Show Equipment**, **Show Movement**, **Show Hit Marker**, **Show Damage Numbers**, **Show Kill Feed**, and **Show Damage Direction** control their corresponding sections.
- Other switches control **Show Silhouette**, the Health/Energy/Hydration labels, **Show Stance**, **Show Volume Indicator**, **Show Weapon Name**, **Show Ammo**, **Show Grenades**, and **Show Stamina Bars**.
- **HUD Scale** ranges from `0.5` to `2`; **HUD Opacity** ranges from `0.2` to `1`.
- **Enable Grid** displays a screen-aligned positioning grid while in a raid.
- **Layout** sliders nudge whole panels: Vitals, Silhouette, Health, Hydration, Energy, Body Status, Effects, Equipment, and Movement.
- **Element Positions** sliders nudge individual effect icons (Light Bleeding, Heavy Bleeding, Fracture, Pain); equipment rows (Weapon, Ammo, Grenades); movement items (Stance, Movement Icon, Stamina, Hand Stamina, Leg Stamina Bar, Hand Stamina Bar); and Kill Feed, Hit Marker, Damage Numbers, and Damage Direction.

Offsets are added to the theme's XML coordinates. Positive X moves right and positive Y moves up. The offset range is `-2000` to `2000` pixels. Offsets are saved across sessions and theme changes; use the configuration manager's reset control for a setting to return it to its default.

## Theme element reference

The attribute descriptions below cover the elements in the shipped themes. Put panel attributes in the named XML element and item attributes on the indicated child element.

### Palette and icon tint

`<Colors>` provides shared semantic colors:

| Attribute | Used for |
| --- | --- |
| `default` | Default/untinted icons |
| `good`, `warn`, `bad` | Positive, warning, and harmful states |
| `text`, `dim` | Main and subdued text |
| `highlight` | Highlight and headshot accent |
| `lost` | Lost/empty health regions |

`<IconColors>` optionally overrides tint for named icons. Attribute names include `weapon`, `assaultRifle`, `assaultCarbine`, `sniper`, `smg`, `lmg`, `pistol`, `shotgun`, `knife`, `ammo`, `smallAmmo`, `mediumAmmo`, `largeAmmo`, `grenade`, `stance`, `movement`, `movementRun`, `stamina`, `handsStamina`, `lightBleed`, `heavyBleed`, `fracture`, and `pain`. Values are hex colors. An omitted tint uses the related theme color or white, as appropriate.

`<Icons enabled="true"/>` enables panel icons. With icons disabled, icon art (including damage-direction indicators) is hidden; text-only rows remain available.

### Vitals: body silhouette, stats, and status

`<Vitals>` attributes:

| Attribute | Purpose |
| --- | --- |
| `show` | Show/hide the whole panel in the theme |
| `anchor`, `x`, `y` | Panel anchor and position |
| `scale` | Panel-specific scale |

`<Silhouette>` attributes:

| Attribute | Purpose |
| --- | --- |
| `show`, `x`, `y` | Enable silhouette and position it within Vitals |
| `prefix` | Prefix for body-part mask filenames |
| `image`, `painkillerImage` | Base and painkiller-state image filenames |
| `outline` | Outline image filename, or `none` to hide the outline |
| `width`, `height` | Rendered silhouette dimensions |
| `opacity`, `baseColor`, `painkillerBaseColor`, `outlineColor` | Base art opacity and tints |
| `flash` | Enable the flash effect on damaged body parts |
| `maskMode` | `alpha` uses PNG transparency; `luminance` derives the mask from dark pixels |
| `maskThreshold`, `maskSoftness` | Luminance-mask cutoff and edge softness |

For prefix `body_minimal`, the loader looks for `body_minimal_silhouette.png`, `body_minimal_outline.png`, and `body_minimal_<Part>.png` for `Head`, `Chest`, `Stomach`, `LeftArm`, `RightArm`, `LeftLeg`, and `RightLeg`. Keep all silhouette and part PNGs on the same canvas and aligned. Place these files directly in the theme folder. If using standard white shapes on transparent backgrounds, use `maskMode="alpha"`; for dark body-part regions, use `maskMode="luminance"`.

Each `<Stat>` configures a displayed stat. Shipped IDs are `Health`, `Energy`, and `Hydration`.

| Attribute | Purpose |
| --- | --- |
| `id` | Stat identifier |
| `label` | Label text; use `@key` to look it up in localization |
| `showLabel` | Show/hide the label (also controlled in F12) |
| `x`, `y` | Position within Vitals |
| `size`, `labelSize` | Value and label font sizes |
| `color`, `labelColor` | Value and label colors. For Health, `color="auto"` colors the value by health level |
| `format`, `suffix` | Value format and appended text; `{cur}` and `{max}` are available |
| `barWidth`, `barHeight` | Bar dimensions; a height of `0` hides the bar |
| `barBackgroundSprite`, `barFillSprite` | Bar PNG filenames in the theme folder |
| `barBackgroundColor` | Optional background tint; the fill uses the stat's `color` |

`<Status show="true" x="0" y="-168" size="11"/>` controls the body-status text visibility, position, and font size.

### Effects

`<Effects>` attributes: `show`, `anchor`, `x`, `y`, and `scale` control visibility and panel placement/scale; `iconSize`, `rowHeight`, and `fontSize` control icon dimensions, row spacing, and text size; `colorOverlay` enables or disables severity-based icon tint.

Individual PNGs are named with an `Icon` suffix on the attribute. For example: `lightBleedIcon="bleed_light.png"`. Supported effect icon attributes are `lightBleedIcon`, `heavyBleedIcon`, `freshWoundIcon`, `fractureIcon`, `painIcon`, `handsTremorIcon`, `mildMusclePainIcon`, `severeMusclePainIcon`, `concussionIcon`, `tunnelVisionIcon`, `dehydrationIcon`, `energyFatigueIcon`, `overweightIcon`, `criticalOverweightIcon`, `fatigueIcon`, `stunIcon`, `flashIcon`, `painkillersIcon`, `buffIcon`, `debuffIcon`, `berserkIcon`, `poisoningIcon`, and `panicAttackIcon`. Put these files in the theme's `Icons` subfolder. The icon's text color is based on the effect severity and cannot be set per effect through `theme.xml`.

### Equipment

`<Equipment>` attributes:

| Attributes | Purpose |
| --- | --- |
| `show`, `anchor`, `x`, `y`, `scale` | Visibility, anchor, position, and scale |
| `cards` | `true` uses side-by-side cards; `false` uses stacked rows |
| `weaponPanelWidth`, `ammoPanelWidth`, `grenadePanelWidth`, `panelHeight`, `panelGap` | Card dimensions and spacing |
| `iconSize`, `weaponIconSize`, `ammoIconSize`, `grenadeIconSize` | Row/card icon dimensions |
| `rowHeight` | Spacing between items in row layout |
| `fontSize` | Weapon, ammo, and grenade text size |
| `ammoSize` | Legacy attribute; does not change the ammo text size |
| `weaponTextAlign`, `ammoTextAlign`, `grenadeTextAlign` | Card text alignment (`Left`, `Center`, or `Right`) |
| `weaponIcon`, `ammoIcon`, `grenadeIcon` | Base icon filenames |
| `grenadeLabel` | Grenade label, which may use an `@localization.key` |
| `assaultRifleIcon`, `assaultCarbineIcon`, `sniperIcon`, `shotgunIcon`, `smgIcon`, `lmgIcon`, `pistolIcon`, `knifeIcon` | Weapon-class icon filenames |
| `smallAmmoIcon`, `mediumAmmoIcon`, `largeAmmoIcon` | Ammo-class icon filenames |

Equipment PNGs belong in the theme's `Icons` subfolder. Each class uses the matching class-specific icon when provided; otherwise the base icon or fallback icon is used. The F12 **Show Weapon Name**, **Show Ammo**, and **Show Grenades** switches control those items.

### Movement and stamina

`<Movement>` attributes:

| Attributes | Purpose |
| --- | --- |
| `show`, `anchor`, `x`, `y`, `scale` | Visibility, anchor, position, and scale |
| `iconSize`, `stanceIconSize`, `speedIconSize`, `staminaIconSize`, `handsStaminaIconSize` | Icon sizes |
| `fontSize` | Stance, movement, and stamina text size |
| `showStanceText`, `showSpeedText`, `showStaminaLabels` | Enable corresponding text |
| `stanceX`, `stanceY`, `speedX`, `speedY` | Stance and movement icon/text coordinates within the panel |
| `staminaX`, `staminaY`, `handsStaminaX`, `handsStaminaY` | Stamina text coordinates |
| `staminaIconX`, `staminaIconY`, `handsStaminaIconX`, `handsStaminaIconY` | Stamina icon coordinates |
| `staminaBarX`, `staminaBarY`, `handStaminaBarX`, `handStaminaBarY` | Leg and hand stamina bar positions |
| `staminaBarWidth`, `staminaBarHeight`, `handStaminaBarWidth`, `handStaminaBarHeight` | Leg and hand bar dimensions |
| `staminaBarFillDirection`, `handStaminaBarFillDirection` | `LeftToRight` or `RightToLeft` fill |
| `staminaBarBackgroundSprite`, `staminaBarFillSprite` | Shared bar background and fill PNGs |
| `staminaBarBackColor` | Background sprite tint |
| `iconColorOverlay` | Tint stamina icons using `IconColors` |

Icon filename attributes are `stanceIcon`, `crouchIcon`, `proneIcon`, `walkIcon`, `sprintIcon`, `speedIcon0` through `speedIcon5`, `staminaIcon`, and `handsStaminaIcon`. Put PNGs in the theme's `Icons` folder. If the walk icon is absent, the standing icon is reused. The F12 **Show Stance**, **Show Volume Indicator**, and **Show Stamina Bars** switches can override visibility.

### Hit marker, damage numbers, and damage direction

`<HitMarker>`:

| Attribute | Purpose |
| --- | --- |
| `show` | Enable the marker |
| `style` | `X` or `Cross` |
| `image`, `crossImage` | PNG paths for the two styles; paths are relative to the theme folder (usually under `Icons/`) |
| `thickness`, `length`, `gap` | Marker stroke thickness, arm length, and center gap |
| `duration`, `punch` | Display time and scale-punch effect |
| `color`, `headshotColor`, `killColor` | Normal-hit, headshot, and kill colors |

The F12 **Hit Marker Thickness** overrides XML thickness when greater than zero (config range `0` to `8`; `0` uses the theme value). Marker X/Y offsets are available under **Element Positions**.

`<DamageNumbers>` accepts `show`, `size` (font size), `life` (seconds shown), `rise` (travel distance), and `spawnRise` (initial rise above the hit point). Damage-number colors are `color`, `headshotColor`, and `killColor`. Use the F12 visibility switch and Element Positions offsets to adjust visibility and placement.

`<DamageDirection>` accepts `show`, `image`, `radius` (distance from screen center), `length` (icon length), `life` (seconds shown), and `color`. Its image is loaded from the theme's `Icons` folder with the same fallback behavior as other icons. It is not shown when `<Icons enabled="false"/>`.

### Kill feed

`<KillFeed>` attributes: `show`, `anchor`, `x`, `y`, and `scale` control visibility and placement/scale; `max` sets the number of retained lines; `life` sets each line's lifetime in seconds; `size` sets text size; `spacing` sets line spacing; `grow` is `Up` or `Down`; and `format` / `headshotFormat` specify normal and headshot lines.

Format text supports `{name}`, `{weapon}`, `{distance}`, `{distanceUnit}`, `{part}`, `{kills}`, `{level}`, and `{tag}` substitutions. Rich-text tags include `{c:#RRGGBB}...{/c}` for color, `{b}...{/b}` for bold, `{i}...{/i}` for italics, and `{size:N}...{/size}` for size. The headshot format supports `{t:killfeed.headshot}` for the localized headshot label. Use F12 **Element Positions** to nudge the feed.

## PNG artwork and fallback rules

PNG art can be replaced by an image with the same filename, or assigned a new filename in the theme XML. For regular theme sprites (silhouettes and bars), files are placed next to `theme.xml`. For icons, files are placed in `Icons/`.

Icon lookup first checks the selected theme, then the matching path in `Themes/Minimal`, then `Themes/Minimal/Icons/default.png`. A missing silhouette part mask is simply not rendered. Keep transparency where needed and maintain consistent image dimensions/alignment for layered silhouette images.

## Sound support and customization

Hit sounds are loaded from the plugin's `Audio` folder. Replace the existing files while keeping these exact names:

| Filename | Plays when |
| --- | --- |
| `bullet-hit.wav` | A non-headshot hit that is not lethal |
| `bullet-headshot.wav` | A non-lethal headshot |
| `bullet-kill.wav` | A lethal hit, including a lethal headshot |

Use uncompressed PCM WAV files. Supported bit depths are 8-, 16-, 24-, or 32-bit PCM; mono and stereo are supported. Keep a non-zero sample rate and valid audio data. Compressed WAV, MP3, OGG, and other formats are not accepted by this loader. After replacing files, reload the plugin/game or re-enter a raid so clips are reloaded.

**Play Hit Sounds** enables/disables sound playback (including the F10 audio preview). **Hit Sound Volume** ranges from `0` (silent) to `2` (amplified); the default is `1`. F10 outside a raid tests a randomly selected loaded clip; in a raid it also opens the HUD preview. A missing or invalid sound file is reported in the BepInEx log.

## Localization support

Localization catalogs are XML files in `BepInEx/plugins/HudBreakout/Localization/`, one file per locale, named with the locale identifier (for example, `en.xml`, `fr.xml`, or `zh-CN.xml`). Catalogs have a `<Localization>` root and `<Text>` entries:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Localization>
  <Text key="stats.health">Health</Text>
  <Text key="equipment.grenades">Grenades</Text>
  <Text key="killfeed.headshot">HEADSHOT</Text>
</Localization>
```

To create a translation, copy `en.xml`, rename the copy to the locale name (e.g. `fr.xml`), and translate each entry's text while preserving every `key`. Add the catalog before starting the game; available catalogs are discovered at plugin initialization and appear in the **Language** selector. Choose `Auto` to use the operating system display language when a matching catalog is present. Otherwise, the game uses English. Missing translation keys fall back to English; a missing English key is displayed as the key and logged.

The mod's catalogs are for HudBreakout-owned text, including config labels/descriptions, effect names, stat labels, grenade text, and kill-feed strings. Game-provided item names continue to use the game's own localization.

In `theme.xml`, prefix a label with `@` to look it up in a catalog; for example, `<Stat ... label="@stats.health"/>`. Without `@`, a theme label is literal text. The headshot format token `{t:killfeed.headshot}` inserts the localized headshot label; `{distanceUnit}` uses the localized distance unit. Config-language changes rebuild the HUD, but config-manager labels/descriptions may require restarting the plugin/game to refresh.
