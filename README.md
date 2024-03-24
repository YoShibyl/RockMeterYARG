# Rock Meter mod for YARG

![rock meter 0 4 0](https://github.com/YoShibyl/RockMeterYARG/assets/18250695/1488687c-1ce1-4a8a-9713-051daeb34bab)

A BepInEx 6 mod for YARG v0.12.1+ that adds a Rock Meter, a Combo Meter, and the ability to fail songs

## Currently tested and working versions
| Stable   | Nightly (recommended) |
|----------|-----------------------|
| v0.12.1  | b2353                 |

## Disclaimer
As this is a modification to the game, bugs may occur and performance may be slightly impacted.  Also, I'm not that great at coding, so if you can improve this, feel free to help out.

### While this mod and BepInEx are installed, please *DO NOT* report any bugs to YARC, *unless* you can reproduce them in the unmodded game!

## Known issues
- The mod currently only works properly with one player at a time, so please don't use this mod with multiple players!
- Clicking and dragging the Rock Meter doesn't always work due to the hitbox being half the size of the actual meter for some reason

## Installation
1) Install [BepInEx 6.0.0-pre1](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.1) to a ***portable installation*** of YARG.
    - You can download the latest nightly build of YARG [here](https://github.com/YARC-Official/YARG-BleedingEdge/releases) and then extract it to its own folder.
2) Download and extract the latest release of the mod from [Releases](https://github.com/YoShibyl/RockMeterYARG/releases)
3) Launch `YARG.exe`, and then load a song with only ONE (1) five-fret guitar player active.
    - You should see the Rock Meter appear on the right side of the screen, somewhere below the score counter.  If not, then either you didn't properly install the mod, or an update could have broken it.
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

## Credits and thanks
- EliteAsian123 : Created YARG
- Everyone who has worked on YARG
- The Clone Hero / YARG community
- This mod was created by YoShibyl

**Shout-out to these streamers:**
- Acai : [Twitch](https://twitch.tv/Acai)
- JasonParadise : [Twitch](https://twitch.tv/JasonParadise)
- randyladyman : [Twitch](https://twitch.tv/randyladyman)
- YoShibyl AKA "Yoshi" (me) : [Twitch](https://twitch.tv/Yoshibyl)
