# Original game rules: missions, score and HUD (work in progress)

Goal: the original GTA 1 game rules 1:1, with only the camera and twin-stick controls changed.
The rules come from the original data files and reverse engineering of the original gta.exe.
Nothing is invented: a command that isn't recovered yet stops its mission step and is logged; it never fakes success.

## In the engine now (`src/mission/`, part of perspective-lock-android.patch)
- `MissionScript`: parses the original GTADATA/MISSION.INI (all 18 chapters, 0 unparsed lines).
- `MissionVM`: the mission command interpreter (32-slot scheduler, 150 original command names).
- `GameEconomy`: score/money, multiplier, lives, wanted points (thresholds 151/251/351/501), rewards.
- `CarnageMissionWorld`: connects the interpreter to the engine (objects, map coordinates, HUD text, arrows).
- `MissionSession`: loads the chapter for the selected city and runs it 25 times a second.
- `OriginalCamera`: the original game's own camera (follow and zoom rules), used only to answer the script's
  "is it on screen" questions the way the original would. The view you play with is unaffected.
- HUD: money, multiplier, lives and wanted level come from GameEconomy.
- Original front-end, saves, chapter progression, radio and music: see the other docs.

## What a chapter does now (checked on the emulator with real traffic, Liberty City chapter 1)
- You start where the original starts you (the script's PLAYER position and facing), next to your parked car.
  The script's parked cars (PARKED) are created when the chapter loads.
- The opening script runs: target arrow, pager briefing, phones enabled, the pickups around the city appear.
- Walking to the ringing phones and answering starts the mission; its car appears where the script says, the
  arrow points at it, and the mission waits for you to get in.
- Starting a new phone mission or a RESET clears leftover mission cars and objects the way the original does:
  they stay only while the original screen would still show them.

## Rules recovered from gta.exe in this area
- All 72 script object types and what the loader stores for each (value / second value / trigger).
- Trigger records: handle order, enable/disable for every type, re-arming when a triggered script ends.
  Working trigger types: area (TRIGGER), phone group (MPHONES), CARTRIGGER (you are driving that mission car,
  anywhere), CORRECT_CAR_TRIG (that car inside the area), CORRECT_MOD_TRIG (that car model inside the area).
- Positions for every object type (one routine in the original), so ARROW, GOTO, LOCATE and GENERAL_ONSCREEN
  work for all of them. Phones and pickups sit in the centre of their map square, as in the original.
- Heights: the original measures downwards; streets are its layer 3. One conversion is used everywhere
  (this fixed phones drawn two levels up, pickups under the road, and mission cars placed below a raised road).
- PARKED_ON for every ordinary car model, with the original's clearing of the parking square first (cars
  touching the square are removed unless a player drives them; long cars also clear the next square).
- MAKEOBJ (flags, briefcases, drop-off markers), CANCEL_BRIEFING, STARTUP, LOCATE, DEAD_ARRESTED (death only,
  see below), counters on any object type (DECCOUNT/INCCOUNT/COMPARE), ENABLE for plain doors.
- Every pickup type can be placed. Collecting: weapons, armour, bribe, lives, multiplier, jail-free card and
  help messages work; the speed pickups (types 6-8) are placed but not collectable yet.

## Not yet working
- PARK (the car drives itself into the garage and you are put out) - the first mission stops there.
- Mission pedestrians (PED_ON and everything that needs them), cranes, car bombs, remaining trigger types
  (gun, damage, car-destroyed, moving, bomb shop, respray).
- Police (`src/police/`): wanted thresholds per section, response levels, arrest-vs-death penalties, lives and
  ranked respawn points are from the original. Not live yet: pursuit/roadblock control, arrest trigger and
  radius, wanted-level decay. DEAD_ARRESTED therefore only sees death, not arrest.
- The original camera's car speed input is converted from the engine's speed (the original unit is taken as
  pixels per update); on foot it is exact.
- Slopes and "flat" map squares as car spawn points; one script car in Liberty City chapter 1 is on one.
- Car collision damage counts during an impact, not for every physics frame of a lasting contact. The
  original's exact damage rule is still to be recovered.
