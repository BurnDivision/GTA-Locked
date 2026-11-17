# Original game rules: missions, score and HUD (work in progress)

Goal: the original GTA 1 game rules 1:1, with only the camera and twin-stick controls changed.
The rules come from the original data files and reverse engineering of the original gta.exe.
Nothing is invented: a command that isn't recovered yet stops its mission step and is logged; it never fakes success.

## In the engine now (`src/mission/`, part of perspective-lock-android.patch)
- `MissionScript`: parses the original GTADATA/MISSION.INI (17 sections, 0 unparsed lines).
- `MissionVM`: the mission command interpreter (32-slot scheduler, 150 original command names).
- `GameEconomy`: score/money, multiplier, lives, wanted points (thresholds 151/251/351/501), rewards.
- `CarnageMissionWorld`: connects the interpreter to the engine (objects, map coordinates, HUD text, arrows).
- `MissionSession`: loads section [1] (Liberty City, Easy) for NYC.CMP at game start and runs it every frame.
- HUD: money, multiplier, lives and wanted level come from GameEconomy (were fixed placeholder values).
- The game starts in Liberty City (NYC.CMP) as the original does, when that map is present.

- Payphones (M1-9b): placed as the original phone object; answer box +-16/64 cell, range +-15 cells, ring trigger +-3 cells
  (recovered from gta.exe, no height/speed test). The mission interpreter runs at the original 25 updates/s.

## Not yet working
- The first mission chain can't finish in-game: original player states 6/7, RESET cleanup and PARKED_ON car creation
  are not recovered yet, so those steps block.
- The score/lives/multiplier HUD panel is not visible on Android (pre-existing, under investigation).
- Police pursuit, arrest, death/respawn, pickups, frenzies, front-end menu, saves and radio.
