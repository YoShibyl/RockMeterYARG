# Themes
***(Coming soon in v0.7.0)***

In the config menu for v0.7.0 and newer, you'll be able to load custom themes for the Rock Meter and Combo Meter.

## How to make a theme
To start, create a folder for your theme, preferably within the `BepInEx/plugins/assets/themes` folder in your YARG installation.

Next, you're going to want to add a `theme.ini` file to said theme folder.  For reference, a `theme.ini` is included in the `BepInEx/plugins/assets` folder, starting with v0.7.0, which should look something like this:
<details open>
  <summary>theme.ini - v0.7.0-pre2</summary>

```ini
[Meta]
theme_name = Example Theme
creator = Your name
description = Lorem ipsum

;; Version 0.7.0-pre2

[Rock Meter]
health_scale = 1
max_needle_angle = 88

[Combo Meter]
combo_scale = 1
max_digits = 6
force_basic_combometer = false

[Colors]
enable_health_color = true
enable_combo_color = true

default_combo_bg = 444444
default_combo_text = ffffff
default_combo_edge = 7f7f7f

default_health_meter = f9bc75
default_health_overlay = ffffff
```
</details>

### Options and their values
- **`health_scale`** : Controls the size of the Rock Meter.
  - Default: 1 (`float`)
- **`max_needle_angle`** : The maximum needle rotation from the default position of the Rock Meter, in degrees.  Useful if your theme uses a design with a different angular range.
  - Default: 88 (`float`)
- **`combo_scale`** : Like `health_scale`, controls the size of the Combo Meter.
  - Default: 1 (`float`)
- **`max_digits`** : Controls the maximum number of digits to display on the Combo Meter, ranging from 4 to 9.  For example, if set to 7, then the Combo Meter won't display values past 9,999,999.
  - Default: 6 (`int`)
- **`force_basic_combometer`** : Controls whether to force load `combo_meter.png` instead of separate base and edge assets.  This is useful if you want to use a single image for the Combo Meter.
  - Default: false (`bool`)
- **`enable_combo_color`** and **`enable_health_color`** : Control whether to allow custom colors for the Combo Meter and Rock Meter, respectively.
  - Default for both: true (`bool`)
- The **`default_combo_*`** and **`default_health_*`** options : All of these control the default colors to load when resetting the meter colors in the Config Menu.
  - Default values can be found in the example config above, as hex color codes (`RRGGBB`, where RGB = red, green, blue)

### Assets
The key to making a theme's design work properly is to center the assets around the center of rotation for the needle, which should be at the very center of the image.  Otherwise, your rock meter will animate weirdly.  Also, be sure to properly set the `max_needle_angle` according to your theme's design.

Of course, you don't need to include any assets in your theme at all, since the mod will fallback to the default assets when not found in your theme's folder.  But it would be a good idea to at least include some assets.

**All the assets that change color should be grayscale and mostly white so that custom colors can render properly!**

- **`combo_bg.png`** : The combo meter's center/background.
- **`combo_edge.png`** : The combo meter's edge/border.
- **`combo_meter.png`** : A basic, single-image fallback for the combo meter.  Will be used if `force_basic_combometer` is set to true.
- **`health_meter_base.png`** : The body of the Rock Meter, usually colorable.
- **`health_meter_ryg.png`** : The part of the Rock Meter that shows the red/yellow/green zones, indicating how well the player is performing.
- **`meter.png`** : A basic Rock Meter image to fallback to, in case the previous two assets are missing.
- **`needle.png`** : The needle of the Rock Meter.  Should be pointing to the left, with the base of rotation at the center of the image.
