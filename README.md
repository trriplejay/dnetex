# Software Factory Survivors

A survivors-style game written in C# with [MonoGame](https://monogame.net/) (DesktopGL). It runs on macOS, Linux and Windows. It's an early work in progress.

Current features: the game runs full screen with a lime ship shaped like the Quarterback q/b logo (an infinity symbol with a vertical line through it) in the middle. W/A/S/D fly the ship up, left, down and right on screen, and the ship always turns to face the mouse pointer instantly, so it fires toward the mouse. It fires a laser from its nose once a second, faster with the attack speed upgrade.

Enemies are white line-art logos of AI companies, each with its own hit points, speed and movement pattern. They spawn just off a random screen edge, and tougher types join as the clock runs. Each type has its own spawn rate (spawns per minute) that can be changed mid-game through `GameSession.Spawner.SetSpawnsPerMinute`. All enemy stats live in `EnemyRoster.cs`.

| Enemy | HP | Movement | XP | Arrives |
|---|---|---|---|---|
| Gemini | 3 | Chases | 2 | 0:00 |
| Hugging Face | 1 | Chases fast, in swarms | 1 | 0:30 |
| ChatGPT | 4 | Weaves side to side | 3 | 1:00 |
| Claude | 3 | Creeps, then lunges in a straight line | 3 | 1:30 |
| Grok | 4 | Spirals in fast | 4 | 2:00 |
| Copilot | 6 | Weaves with a tight, quick sway | 5 | 3:00 |
| Cursor | 8 | Starts slow and keeps accelerating | 7 | 4:00 |
| Perplexity | 7 | Creeps and teleports closer | 6 | 5:00 |
| Apple Intelligence | 10 | Spirals in wide | 9 | 6:00 |
| Meta | 20 | Slow tank | 15 | 7:00 |

**Bosses.** At 2:00 the Gemini Boss enters: a Gemini logo 4× normal size with 40 hit points that crawls toward you at a third of a regular Gemini's speed. Every 3 seconds it throws a regular Gemini at a random spot 80–180 px from you; the thrown Gemini flies there at 4× Gemini speed, then lands and chases normally. It gets more dangerous as it is worn down: at 75% health it throws 3 Geminis at a time and they fly twice as fast (8× Gemini speed); at 50% it throws 5; at 25% it throws two waves of 5, 0.25 s apart, and starts moving like Claude (creeping, then lunging) at 4× its own speed. Phases are data on the enemy type (`EnemyType.Phases`), so any boss can have them. While a boss is alive no other enemies spawn; regular spawning resumes once it's defeated. A boss isn't destroyed by touching the ship, and its health bar runs down the right side of the screen, notched at each phase. Bosses are scheduled in `EnemyRoster.Bosses`.

Kills give experience; level *n* needs 5 × *n*^1.5 experience (5, 14, 26, 40, 56 …). Each level pauses the game and offers three random upgrade cards; the one you pick lasts for the rest of the game:

| Upgrade | Effect per pick |
|---|---|
| Attack speed | +0.25 volleys per second (starts at 1) |
| Projectile size | Lasers +50% wider and longer |
| Projectiles | +1 laser per volley, fanned out (max 9) |
| Damage | +1 damage per hit |
| Piercing | Lasers pass through +1 more enemy |
| Max HP | +1 max hit point (and heals 1) |

The ship starts with 1 hit point, so the first touch is fatal. With extra hit points, a touch costs 1 hit point, destroys the enemy, and makes the ship blink and ignore contact for 1 second.

A clock at the top of the screen counts up in minutes and seconds; your level, experience bar and hit points are in the top-left corner. Survive until 10:00 to win. Each enemy killed is worth 1 point. If an enemy touches the ship first, the game freezes and shows GAME OVER. On a win you can type up to three initials to save your score to the high-score table, which lasts until the game is closed.

## Controls

| Key | Action |
|---|---|
| W / A / S / D | Fly up / left / down / right (diagonals work and are no faster) |
| Mouse | Aim: the ship faces and fires toward the pointer |
| 1 / 2 / 3 or click | Pick an upgrade card after levelling up |
| A–Z, Backspace | Type initials after a win |
| Enter | Save your initials after a win; start a new game after GAME OVER or from the high-score table |
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

For debugging, arguments after `--` choose where the game starts:

```sh
dotnet run --project src/SoftwareFactorySurvivors.Desktop -- --time 2:00 --level 5
```

- `--time MM:SS` (or whole seconds) starts the clock there, with every enemy type that has arrived by then. A boss due at exactly that time enters straight away; bosses due earlier are skipped.
- `--level N` starts the ship at level N, offering the upgrade cards for each skipped level before play begins.

Both apply again when you restart with Enter.

## Publishing a standalone build

To produce a folder that runs without .NET installed, pick the runtime identifier for the target platform:

```sh
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r osx-arm64   --self-contained   # Apple Silicon Mac
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r osx-x64     --self-contained   # Intel Mac
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r linux-x64   --self-contained   # Linux
dotnet publish src/SoftwareFactorySurvivors.Desktop -c Release -r win-x64     --self-contained   # Windows
```

The output goes to `src/SoftwareFactorySurvivors.Desktop/bin/Release/net10.0/<rid>/publish/`. Run the `SoftwareFactorySurvivors.Desktop` executable (`SoftwareFactorySurvivors.Desktop.exe` on Windows) from that folder.
