# Themes
***(Coming soon in v0.7.0)***

In the config menu for v0.7.0 and newer, you'll be able to load custom themes for the Rock Meter and Combo Meter.

## How to make a theme
To start, create a folder for your theme, preferably within the `BepInEx/plugins/assets/themes` folder in your YARG installation.

Next, you're going to want to add a `theme.ini` file to said theme folder.  For reference, a `theme.ini` is included in the `BepInEx/plugins/assets` folder, starting with v0.7.0, which should look something like this:
<details open>
  <summary>theme.ini</summary>

```ini
[Meta]
theme_name = Default
creator = YoShibyl
description = The default Rock Meter theme for YARG
;;; DEFAULT CONFIG

[Rock Meter]
;; The scale of the rock meter, which multiplies the width and height.
; Default: 1.0
health_scale = 1

;; The maximum clockwise rotation of the needle, in degrees, from the middle.
; Default: 88.0
max_needle_angle = 88

[Combo Meter]
;; The scale of the combo meter
; Default: 1.0
combo_scale = 1

;; Maximum digits to display on combo meter.
;; For example, a value of 4 would result in 9999 being the max displayed streak
; Default: 6
max_digits = 6

;; This setting controls whether to force load `combo_meter.png` instead of separate base and edge assets.
;; Useful for when you want to use just one image for the combo meter.
; Default: false
force_basic_combometer = false

[Colors]
;; Whether to allow combo meter coloring.
; Default: true
enable_combo_color = true

;; Whether to allow rock meter coloring.
; Default: true
enable_health_coloring = true

;;; The default colors for this theme, in hex (RRGGBB)
;; Combo base color (combo_bg.png)
; Default: 444444
default_combo_bg = 444444

;; Combo text color
; Default: FFFFFF
default_combo_text = FFFFFF

;; Combo edge color (combo_edge.png)
; Default: 7F7F7F
default_combo_edge = 7F7F7F
```
</details>

### Options and their values
*I'll fill this in soon™*
