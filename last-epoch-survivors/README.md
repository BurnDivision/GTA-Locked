# LE Survivors

A Survivors-style game mode for **Last Epoch**, built as a MelonLoader mod. It plays like
Soulstone Survivors: your skills cast themselves while you move, the enemy hordes keep
growing, you pick from rarity-tiered upgrades on level-up, tag Resonances stack into
build synergies, skills Ascend, Omens (curses) raise the difficulty, and the Obelisk holds
permanent meta progression. It all runs on Last Epoch's own classes, skills, ailments, stats and enemies.

> Status: **in development**. The core game logic is complete and covered by tests (38 tests, including pacing
> simulations). The in-game layer builds and loads in the game, but in-game play-testing is in progress.
> See [docs/STATUS.md](docs/STATUS.md).

## Requirements
- Last Epoch (Steam, the public build) on Windows. Tested against Unity 6000.4.8f1 (game build of 2026-11-18).
- [MelonLoader](https://github.com/LavaGang/MelonLoader) **0.7.3**
- .NET 6 runtime (MelonLoader installs it if it's missing)
- To build: the .NET SDK (8 or later)

## Install
1. **Back up your saves**: `%USERPROFILE%\AppData\LocalLow\Eleventh Hour Games\Last Epoch\Saves`.
2. Install MelonLoader 0.7.3 into the game folder, launch the game once so it generates its assemblies, then quit.
3. **Unity 6000.4 fix.** MelonLoader 0.7.x writes broken interop assemblies for Unity 6000.4 (they contain duplicate
   `<>O` types), and then logs `No Support Module Loaded!`. Run the bundled fixer once after every regeneration:
   ```
   dotnet run --project tools/InteropFix -- "<Last Epoch>\MelonLoader\Il2CppAssemblies"
   ```
   It renames the duplicates and keeps a `.bak` of every file it changes.
4. Build and deploy the mod:
   ```
   dotnet build src/LESurvivors.Mod -c Release -p:GameDir="<Last Epoch folder>"
   copy src\LESurvivors.Mod\bin\Release\net6.0\LESurvivors.dll       "<Last Epoch>\Mods\"
   copy src\LESurvivors.Mod\bin\Release\net6.0\LESurvivors.Core.dll  "<Last Epoch>\UserLibs\"
   ```

## Play
1. Load an **offline** character. A fresh level-1 character is what the balance targets. The mode is offline-only.
2. Stand on open ground. The Arena of Memory is ideal.
3. Press **F6** to open the Survivors menu, pick an Epoch tier (and Omens once you've unlocked them), then press **Begin Run**.
4. Move to survive. Skills fire automatically. Collect XP motes and choose upgrades (keys **1–4** pick a card).
   Elites drop Caches, which hold upgrades and relics. Wardens appear at 5, 10 and 15 minutes, and the Harbinger at 20:00.
5. **P** pauses. Results and Time Shards are saved to `UserData/LESurvivors/profile.json`.

### What a run never touches
- **Character XP, items and gold.** Run enemies give no LE experience or loot. The run has its own levels and rewards.
- **Your character's life.** During a run you cannot really die. Defeat ends the run instead, which keeps
  **Hardcore characters safe**.
- **Stats.** All run stats are temporary buffs that are removed when the run ends.

## Uninstall
Delete `Mods\LESurvivors.dll` and `UserLibs\LESurvivors.Core.dll`. To remove MelonLoader as well, delete `version.dll`,
`MelonLoader\`, `Mods\`, `Plugins\`, `UserData\` and `UserLibs\` from the game folder. Steam's "Verify integrity of game files" restores everything else.

## Layout
| Path | What |
|---|---|
| `src/LESurvivors.Core` | Engine-independent game logic: run, build/stats, upgrades, director, relics, omens, meta profile, simulator |
| `src/LESurvivors.Mod` | MelonLoader mod: the Last Epoch adapter (`Game/LE.cs`), Harmony patches, run host, IMGUI screens, dev console |
| `tests/` | xUnit tests, including headless pacing simulations (`dotnet test tests/LESurvivors.Core.Tests`) |
| `tools/InteropFix` | Repairs MelonLoader's interop output on Unity 6000.4 |
| `tools/ApiDump` | Writes a searchable outline of the game's interop assemblies (output stays local; never commit it) |
| `docs/DESIGN.md` | Game design |

## Rules this project follows
- Offline play only. The mod never alters online play, anti-cheat or accounts.
- It's built only against the public game build. Nothing here comes from the Community Test build.
- No game files, generated assemblies or extracted assets are committed or redistributed.
