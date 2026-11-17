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

## Not yet working
- Payphones are loaded as positions only: they don't ring and can't be answered, so missions can't start yet.
- Police pursuit, arrest, death/respawn, pickups, frenzies, front-end menu, saves and radio.
