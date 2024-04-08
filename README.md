# Rock Meter mod for YARG
<details open>
<summary>Default Rock Meter (v0.7.0)</summary>

![Rock Meter 0 7 0](https://github.com/YoShibyl/RockMeterYARG/assets/18250695/cf236b94-760b-4681-8af5-9de22b9e10e1)
</details>

A BepInEx 6 mod for YARG v0.12.2+ that adds a Rock Meter, a Combo Meter, and the ability to fail songs.

As of v0.7.1 of the mod, only YARG v0.12.2 and newer are supported.

## Disclaimer
As this is a modification to the game, bugs may occur and performance may be slightly impacted.  Also, I'm not that great at coding, so if you can improve this, feel free to help out.

## Currently tested and working YARG versions
| Stable   | Nightly (recommended) |
|----------|-----------------------|
| v0.12.2  | b2412                 |

### While this mod and BepInEx are installed, please *DO NOT* report any bugs to YARC, *unless* you can reproduce them in the unmodded game!

## Known issues
- Clicking and dragging the Rock Meter or Combo Meter doesn't always work due to the hitbox being half the size of the actual meter for some reason

## Installation
1) Install [BepInEx 6.0.0-pre1](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.1) to a ***portable installation*** of YARG.
    - You can download the latest nightly build of YARG [here](https://github.com/YARC-Official/YARG-BleedingEdge/releases) and then extract it to its own folder.
2) Download and extract the latest release of the mod from [Releases](https://github.com/YoShibyl/RockMeterYARG/releases)
3) Launch `YARG.exe`, and then load into a song.
    - You should see the Rock Meter appear on the right side of the screen, somewhere below the score counter.  If the meter doesn't appear, then either you didn't properly install the mod, or an update could have broken it.
    - Updates that change `YARG.Core` may hypothetically break the mod, so keep that in mind when updating to a nightly build newer than what is listed above.

### Updating
To update to a newer nightly build of YARG, simply copy over the files of the update to your portable modded installation.

If an update causes the mod to stop working, please let me know!

### Uninstalling
To uninstall the mod, remove the following files and folders from the game's folder:
- `winhttp.dll`
- `doorstop_config.ini` *(optional)*
- The `BepInEx` folder *(optional)*

## Usage
This mod has its own configuration file, located at `BepInEx/config/com.yoshibyl.RockMeterYARG.cfg`.  Moving the Rock Meter's position will automatically save the position to the config.  Additionally, you can change whether or not to stop the song on fail, as well as enable/disable the Rock Meter and/or the Combo Meter, through the config file.

As of v0.6.0, the config options can be edited in-game via the config menu by clicking the Rock Meter version text, which is located next to the version watermark in the top-right corner.

### Themes (v0.7.0+)
Learn how to make themes [here](https://github.com/YoShibyl/RockMeterYARG/blob/main/Docs/Themes.md)

## Credits and thanks
- EliteAsian123 : Created YARG
- Everyone who has worked on YARG
- The Clone Hero / YARG communities
- rickyah : Created the [INI parser](https://github.com/rickyah/ini-parser), used in v0.7.0+
- This mod was created by YoShibyl

**Shout-out to these streamers:**
- Acai : [Twitch](https://twitch.tv/Acai)
- JasonParadise : [Twitch](https://twitch.tv/JasonParadise)
- randyladyman : [Twitch](https://twitch.tv/randyladyman)
- YoShibyl AKA "Yoshi" (me) : [Twitch](https://twitch.tv/Yoshibyl)
