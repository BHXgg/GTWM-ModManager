<h1 align="center">GTWM Mod Manager</h1>

<p align="center">Enable, disable and hot-reload GTWM mods from inside the game, with a live log viewer.</p>

<p align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE) ![Game](https://img.shields.io/badge/Get%20To%20Work-1.1.1.12-orange) ![Unity](https://img.shields.io/badge/Unity-6000.0%20Mono-black) ![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4) ![Platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey) [![Requires GTWM 1.3.1+](https://img.shields.io/badge/requires-GTWM%201.3.1%2B-2ea44f)](https://github.com/BHXgg/GTWM)

</p>

A mod for **Get To Work**, built on the [GTWM mod loader](https://github.com/BHXgg/GTWM).

## Features

- Lists every installed mod with its version, author, description and status.
- **Enable / disable / reload** any mod without restarting the game.
- **Scan for new mods** dropped into `GTWM\mods` while the game runs.
- **Auto-reload changed DLLs**: rebuild a mod and it reloads by itself, which is ideal while developing.
- **In-game log viewer** with filtering by level and mod.
- **UI scale** for every GTWM menu (automatic by default, based on resolution).
- Mods that crash repeatedly are disabled automatically, and their error is shown here.

## Installing

1. Install the [GTWM loader](https://github.com/BHXgg/GTWM/releases) (1.3.1 or newer).
2. Download `GTWM-ModManager-1.1.0.zip` from [Releases](https://github.com/BHXgg/GTWM-ModManager/releases).
3. Extract it into the game folder (the one with `Get To Work.exe`). It lands in `GTWM\mods\ModManager\`.
4. Start the game, or press **Scan for new mods** in the Mod Manager if it's already running.

## Usage

Press **F8** in game. Changes are remembered in `GTWM\config\gtwm.modmanager.cfg` (enabled state is kept by the
loader in `GTWM\config\loader.cfg`).

## Configuration

Settings are saved to `GTWM\config\gtwm.modmanager.cfg` (`key = value`). Most can be changed in the menu; for the rest,
edit the file and press **Reload** on the mod in the Mod Manager.

| Key | Default | Meaning |
|---|---|---|
| `toggle_key` | `F8` | Key that opens the manager (Unity `KeyCode` name) |
| `ui_scale` | `0` | GUI scale for all GTWM menus; 0 = automatic |
| `ui_blur` | `true` | Frosted-glass blur behind menus |

## Building from source

**Requirements:** Windows, the [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or newer, git, and an installed
copy of the game. The loader is included as a git submodule in `external/GTWM`.

```powershell
git clone --recursive https://github.com/BHXgg/GTWM-ModManager.git
cd GTWM-ModManager
.\build.ps1 -GameDir "C:\path\to\Get To Work"            # build into .\dist
.\build.ps1 -GameDir "C:\path\to\Get To Work" -Install   # build and copy into the game
.\build.ps1 -Zip                                          # also produce the release zip
```

Already cloned without `--recursive`? Run `git submodule update --init` (`build.ps1` does this for you).

### Pointing the build at your game

The build compiles against the game's own assemblies (`Get To Work_Data\Managed\*.dll`), which are **not** in this
repository. It looks for the game in this order:

1. `-GameDir "X:\path\to\game"` on `build.ps1` (or `-p:GameDir=...` on `dotnet build`)
2. the `GTW_GAME_DIR` environment variable
3. the default Steam folder, `C:\Program Files (x86)\Steam\steamapps\common\Get To Work`

The game folder is the one that contains `Get To Work.exe`.

### Project layout

```
GTWM-ModManager
├── src/ModManager/          the mod
├── external/GTWM/       loader (submodule): API, UI kit, shared build settings
├── build.ps1            build, package, install
└── Directory.Build.props
```

Mods hot-reload: run `.\build.ps1 -Install` while the game is open and press **Reload** in the Mod Manager (or turn on
auto-reload). See the loader's [mod-writing guide](https://github.com/BHXgg/GTWM/blob/main/docs/writing-mods.md).

## AI assistance disclosure

Parts of this project were developed with the help of an AI coding assistant (Anthropic's Claude), mainly the
user-interface layout and some of the multiplayer networking code. Treat it like any other community mod: read the
code if you want to know exactly what it does, and please report anything that looks wrong.

## Disclaimer

This is an unofficial, fan-made project. It is not affiliated with, endorsed by or supported by the developers or
publishers of *Get To Work*. No game files are included in this repository or its releases; you need your own copy
of the game. Mods change how the game runs: back up your saves, and don't report bugs you hit while modded to the
game's developers.

## License

[MIT](LICENSE) © 2026 BHXgg
