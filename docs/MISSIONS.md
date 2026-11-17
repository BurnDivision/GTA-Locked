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
- HUD score/lives/multiplier panel is visible (it was laid out off-screen right; fixed). LOCK/AUTO touch buttons moved
  down so they don't cover it.
- The game starts in Liberty City (NYC.CMP) as the original does, when that map is present.

- Payphones (M1-9b): placed as the original phone object; answer box +-16/64 cell, range +-15 cells, ring trigger +-3 cells
  (recovered from gta.exe, no height/speed test). The mission interpreter runs at the original 25 updates/s.

- Original front-end (M1-6): animated F_LOGO/F_LOWER screens, original F_* fonts and FXT text, Play/Options,
  Liberty City / San Andreas / Vice City with both chapters each (MISSION.INI sections 1/2, 102/103, 202/203),
  options with cancel rollback, original exit credits. Touch, gamepad and keyboard. The automated test harness
  (config/test_input.txt present) skips the menu and starts the default scenario.

## Not yet working
- The first mission chain runs (headless) phone -> RESET -> brief -> PARKED_ON (original car) -> POWERUP_ON (original
  pickup object, help text, sound; ped and stationary-driver contact) -> ARROW -> DOOR -> PLAYER/IS_PED_IN_CAR -> ENABLE
  area trigger, and stops at DOOR_ON130, which is not recovered yet.
- Police (`src/police/`): wanted thresholds per section, response levels, arrest-vs-death penalties, lives (game over on
  the next death at 0 lives) and ranked respawn points, from the original executable. Not live yet: automatic crime
  detection, pursuit/roadblock control, arrest trigger and radius, and wanted-level decay are not recovered.
- Crime intake (P-2/P-3): taking over an occupied car is a crime with the original point value; witness
  escalation and the delayed crime queue follow the original. Other crime events are not connected yet.
- Nonempty RESET cleanup, parked-car overlap/slope variants and some state transitions are not recovered.
- Police pursuit, arrest, death/respawn, pickups, frenzies, front-end menu, saves and radio.
