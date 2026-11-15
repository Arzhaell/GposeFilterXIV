# GposeFilterXIV

*[Version française](README.fr.md)*

A Dalamud plugin for FINAL FANTASY XIV that keeps one of the game's GPose color filters on outside of GPose.

## Using it
Type `/gposefilter` (or open the plugin from `/xlplugins`): a window made of the game's own interface elements opens.

- **Enabled**: keep the filter on.
- **Filter**: the game's GPose list, in the same order and in the game's language. “Standard” means no filter, like in
  GPose.
- **Intensity**: 0 to 100 %, like the GPose slider. As in GPose, 0 % means no filter at all (the area's own filter is
  hidden too).

The filter stays on everywhere, and comes back by itself after loading screens and when you log in again. A line in the
window tells what the plugin is doing.

## How it works
The game draws the filter itself, exactly as in GPose: the plugin writes the values of the chosen filter (from the
game's `ColorFilter` sheet) where GPose writes them, and reserves that place with the game's own function. No shader,
no hook, no color computation of its own for the filter.

The window is built with [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) from the game's interface elements.
So that it closes along with the game's windows (Escape), KamiToolKit hooks the game's window-closing callback; that is
the only hook, and it has nothing to do with the filter.

- **GPose and cutscenes come first**: when they set a filter of their own, the plugin steps aside, then yours comes
  back. In GPose with “Standard”, yours stays.
- **Turning it off** or unloading the plugin gives back the area's own filter.
- **After a game update**, the plugin checks at startup that the game code is still the one it knows. If not, it pauses
  (it says so in the window and once in the chat) and writes nothing until a new version comes out.

Available in English and French (follows the game's language, can be forced in the window).

## Installing
Add this custom repository in `/xlsettings`, **Experimental** tab, then install GposeFilterXIV from `/xlplugins`:

```
https://raw.githubusercontent.com/Arzhaell/ArzhaelXIVPlugins/main/repo.json
```

This address gives you [all my plugins](https://github.com/Arzhaell/ArzhaelXIVPlugins). If you already added this plugin's own address (`https://raw.githubusercontent.com/Arzhaell/GposeFilterXIV/main/repo.json`), it keeps working; with both, each plugin is listed twice ([how to switch](https://github.com/Arzhaell/ArzhaelXIVPlugins#installation)).

## Building
.NET 10 SDK and XIVLauncher (Dalamud) installed. Clone with the KamiToolKit submodule:

```bash
git clone --recurse-submodules https://github.com/Arzhaell/GposeFilterXIV.git
```

```bash
dotnet build -c Release
```

```bash
dotnet test GposeFilterXIV.Tests/GposeFilterXIV.Tests.csproj
```

Some tests read the installed game (read-only): the `ColorFilter` sheet, and `ffxiv_dx11.exe` to check that the plugin
still finds the code it needs. Run them after a game update to know whether the plugin still works, without launching
the game. On a machine without the game, set `GPOSEFILTER_TESTS_WITHOUT_GAME=1`: those tests then check nothing.

Dev plugin: add `GposeFilterXIV\bin\Debug\GposeFilterXIV.dll` to the dev plugin locations (`/xlsettings`,
**Experimental** tab); Dalamud reloads it by itself after each build, even with the game running. It does not load the
version installed from the repository at the same time: disable that one.

## Releasing a new version
Pushing an annotated `vX.Y.Z` tag is all it takes: the GitHub workflow runs the tests, builds the plugin, creates the
release (its notes are the tag message) and updates `repo.json`.

```bash
git tag -a v1.0.0 --cleanup=verbatim -F notes.txt
```

```bash
git push origin v1.0.0
```

## Credits
- [Dalamud](https://github.com/goatcorp/Dalamud) (AGPL-3.0): plugin loading, access to the game.
- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) (MIT): game structures (`EnvManager`, its
  `ColorFilterOverride`).
- [Lumina](https://github.com/NotAdam/Lumina) (WTFPL): reading the game data (`ColorFilter` sheet).
- [EXDSchema](https://github.com/xivdev/EXDSchema): names of the `ColorFilter` sheet columns.
- [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) (MIT, git submodule): the window, made of the game's own
  interface elements.
- [Dear ImGui](https://github.com/ocornut/imgui) (MIT): the user interface library used by Dalamud.
- [xUnit](https://xunit.net/) for the tests.

GposeFilterXIV is released under the **AGPL-3.0** license (see `LICENSE`), like Dalamud.

FINAL FANTASY XIV © SQUARE ENIX CO., LTD. This plugin is not affiliated with or endorsed by Square Enix.
