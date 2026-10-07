# Software Factory Survivors

A survivors-style game written in C# with [MonoGame](https://monogame.net/) (DesktopGL). It runs on macOS, Linux and Windows. It's an early work in progress.

Current features: the game runs full screen with a ship (a triangle) in the middle. W/A/S/D move the ship.

## Controls

| Key | Action |
|---|---|
| W/A/S/D | Move the ship |
| Esc | Quit (and leave full screen) |

## Project layout

```
SoftwareFactorySurvivors.slnx
global.json                                 # pins the .NET SDK (10.0.x)
src/SoftwareFactorySurvivors.Core/          # game logic: plain C#, no MonoGame dependency
src/SoftwareFactorySurvivors.Desktop/       # MonoGame host: window, input, rendering
tests/SoftwareFactorySurvivors.Core.Tests/  # xUnit tests for the game logic
```

Game rules go in `SoftwareFactorySurvivors.Core`, where they can be unit tested without a window or GPU. `SoftwareFactorySurvivors.Desktop` should stay a thin layer that reads input and draws state.

## Prerequisites

You need the **.NET 10 SDK**. MonoGame is pulled from NuGet automatically, and its native dependencies (SDL2, OpenAL) are bundled in the package, so there's nothing else to install.

### macOS

```sh
brew install dotnet
```

Or use the installer from https://dotnet.microsoft.com/download. If you installed with Homebrew and the game can't find the runtime, add this to your shell profile:

```sh
export DOTNET_ROOT="$(brew --prefix dotnet)/libexec"
```

### Linux

Ubuntu 24.04+:

```sh
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
```

Fedora:

```sh
sudo dnf install dotnet-sdk-10.0
```

Any other distro: use Microsoft's install script:

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
```

Running the game requires a desktop session (X11, or Wayland via XWayland) and working OpenGL drivers. Building and running the tests do not, so they work on headless servers and in CI.

### Windows

```powershell
winget install Microsoft.DotNet.SDK.10
```

Check the install on any platform with `dotnet --list-sdks`.

## Build, test, run

From the repository root:

```sh
dotnet build                                                 # build everything
dotnet test                                                  # run the unit tests
dotnet run --project src/SoftwareFactorySurvivors.Desktop    # play the game
```

## Publishing a standalone build

To produce a folder that runs without .NET installed, pick the runtime identifier for the target platform:

```sh
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r osx-arm64   --self-contained   # Apple Silicon Mac
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r osx-x64     --self-contained   # Intel Mac
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r linux-x64   --self-contained   # Linux
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r win-x64     --self-contained   # Windows
```

The output goes to `src/SoftwareFactorySurvivors.Desktop/bin/Release/net10.0/<rid>/publish/`. Run the `SoftwareFactorySurvivors.Desktop` executable (`SoftwareFactorySurvivors.Desktop.exe` on Windows) from that folder.
