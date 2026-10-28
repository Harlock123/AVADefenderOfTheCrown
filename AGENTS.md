# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, the knights, the
scenes and the controls; this file covers how the code is put together and the rules that keep it working.

## What this is

A strategy game of knights and castles in the style of Cinemaware's 1986 Defender of the Crown, in C# on .NET 10 and
Avalonia 12, drawn through Avalonia's `DrawingContext`. A campaign map of England, with action scenes for the joust, the siege, the night
raid and the battle. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). Everything
in it is original: the paintings are made in code, and the sounds and music are synthesised by
`tools/make_sounds.py`.

## Commands

```bash
dotnet build AVADefenderOfTheCrown.slnx           # the SDK version is in global.json (10.0.400 or later)
dotnet test AVADefenderOfTheCrown.slnx            # every test runs headless: no window opens
dotnet run --project src/AVADefenderOfTheCrown    # play it; --no-audio for silence
CROWN_SHOTS=$PWD/Images dotnet test tests/AVADefenderOfTheCrown.Tests --filter ScreenshotTests   # refresh the README's pictures
CROWN_PROBE=1 dotnet test tests/AVADefenderOfTheCrown.Tests --filter PilotProbe   # whole campaigns by the computer player
python3 tools/make_sounds.py                      # rebuild src/AVADefenderOfTheCrown/Sounds (standard-library Python only)
```

The probe also reads `CROWN_GAMES` (how many campaigns, 8 by default) and `CROWN_SEASONS` (the longest, 80).

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVADefenderOfTheCrown/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states, the map orders and the scenes, and holds `Controls` and the fixed-step loop.
  - `Sim.cs` has the campaign rules: money and armies, marching, conquering, castles falling, the other lords' turns.
  - `Realm.cs` lists the lords and the shires; `World.cs` has `Field`, the `Dice` random source, troops and lords.
  - The scenes, each its own class with its own `Dice`: `Joust.cs`, `Siege.cs`, `Raid.cs` (swordplay) and
    `Battle.cs` (the four tactics).
  - `Pilot.cs` is the computer player, used by the attract mode and the tests.
  - `SaveFile.cs` saves the campaign at the start of each of the player's turns, so a game can be continued.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVADefenderOfTheCrown/Views/`: `GameCanvas` (split over partial files) draws the 1024x768 frame and runs the
  60 Hz timer. `Art.cs` and `MapArt.cs` paint the backgrounds and the map once, pixel by pixel, with `Pix.cs`.
  Ctrl+S saves a screenshot to the Pictures folder.
- `src/AVADefenderOfTheCrown/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVADefenderOfTheCrown/Sounds/`: the generated WAV files, copied next to the executable.
- `tests/AVADefenderOfTheCrown.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `GameplayTests.cs` holds most tests;
  `PilotProbe.cs` and `ScreenshotTests.cs` are switched on by the variables above.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` runs `Tick` in fixed 1/60 s steps (`Field.Step`) from an accumulator,
  independent of the frame rate, so tests can play whole campaigns deterministically from a seed. Draw random
  numbers from the world's seeded `Dice` (or the scene's own), never from `Random.Shared`. `Dice` is saved with the
  campaign, so a continued game plays on the same way.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** Up/down on keys and stick must produce the same `Dy` sign the engine
  expects (negative is up the screen). Test new controls through `InputHandler`, not by building `Controls`
  directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. `A_shift_let_go_as_caps_lock_is_let_go` in `GameplayTests.cs` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores and the saved campaign live in the app-data folder
  (`AVADefenderOfTheCrown`); tests pass a fresh folder under the system temp folder for both and run silent
  (`Silent = true`).
- **Save format.** If you add to `World`, add it to `SaveFile.cs` too, or a continued game loses it.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the campaign rules change, run the pilot probe and check the computer player still wins some campaigns.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
