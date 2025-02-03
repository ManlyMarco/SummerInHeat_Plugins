# SummerInHeat_Plugins
Various plugins for the game Summer in Heat (夏のサカり) by Miconisomi.

## Installation
0. Install the latest versions of BepInEx x64 v5, BepInEx.ConfigurationManager and XUnity.AutoTranslator.
1. Download the plugin you want to use from releases.
2. Extract the contents of the zip file into the `GameData` folder of your game.
3. Open the game and go to settings, you should now see a new "Plugin settings" tab as well as a new "None" setting in the "Mosaic type" dropdown.

## Plugin descriptions
### Tweaks
This plugin helps fully translate the game and adds some improvements:

- Adds a hotkey to open the debug menu (needs to be configured in plugin settings)
- Adds a "Plugin settings" tab to the settings menu
- Opening online manual will check if a local manual is available and open it instead (based on language)
- Fixes uncaught exceptions not being logged

### Uncensor
This plugin uncensors the game by removing mosaic effects and replacing textures.

To enable the uncensor after installing it you have to go to the settings menu and in the "Mosaic type" dropdown select the newly added option "OFF".

You can find the uncensored textures inside `SiH_Uncensor\replacements` if you'd like to improve them or make your own.
Seems like only `sectional.png` is used out of the two internal textures.
The textures can be increased in size, but they must be the same aspect ratio as the original textures and the size must be in a power of 2 (e.g. 512x512, 1024x1024, 2048x2048, etc).

### TranslateRedirectedResources
A tool for translating files in the RedirectedResources folder.
Forked from https://github.com/SpockBauru/SpockPlugins_InsultOrder/tree/9b3bae7e7f20f39aa42d58a45838f714b39d40de/TranslateRedirectedResources
