GTA Perspective Lock 0.7.8 - Windows 10/11 x64

Extract the ENTIRE zip into a writable, short ASCII path, for example:
  C:/Games/GTA-Perspective-Lock
Copy your complete original GTA1 GTADATA folder beside the executable:
  GTA-Perspective-Lock.exe
  OpenAL32.dll
  gamedata/config/sys_config.json
  GTADATA/MISSION.INI
  GTADATA/NYC.CMP (and the other original .CMP maps and STYLE*.G24 files)
  GTADATA/F_*.FON and F_*.RAW (original menu fonts/images)
  GTADATA/ENGLISH.FXT (or your edition's text file)
Original GTA data is required and is not included. This front-end expects the
full GTA1 chapter catalog; demo and London data are not supported by this menu.
Keep the original filenames and subfolders, including GTADATA/AUDIO.

Double-click GTA-Perspective-Lock.exe. Requires an OpenGL 3.3 graphics driver
from Intel, AMD or NVIDIA. No console window opens. Startup errors show a dialog;
details are in GTA-Perspective-Lock.log beside the exe (replaced each launch).
Keep OpenAL32.dll beside the exe even when disabling audio. If no audio device
is available the game continues silently. If a Windows loader dialog appears
before the log is created, check that you extracted all files from the zip.

Data paths are relative to the exe, including when launching from a shortcut.
For data elsewhere, set "g_gtadata" in gamedata/config/sys_config.json to
"C:/Games/GTA/GTADATA", or run:
  GTA-Perspective-Lock.exe -gtadata "C:/Games/GTA/GTADATA"
The command line wins. Empty config means GTADATA beside the exe. Use forward
slashes in JSON paths. Use short ASCII paths for the game and external data;
legacy file readers do not support arbitrary Unicode or long Windows paths.
Standard Windows folders are case-insensitive; case-sensitive folders are not
supported. Optional original music: GTADATA/MUSIC/Track1.wav ... Track10.wav,
or MUSIC/Track1.wav ... Track10.wav beside the exe. Missing/unsupported tracks
are logged and do not prevent play; audio files are not supplied.

Menu: arrows navigate/select player, city, chapter and option values; Enter
accepts/saves, Esc goes back (from the main menu, opens credits/quit).
Delete renames the selected profile; type a name, Backspace erases, Enter saves.
R opens profile reset, with a separate confirmation. Left-click menu items,
profile arrows, cities and bottom Back/Play/Save labels; type names on keyboard.
Gamepad menu: d-pad, A/Start accepts, B goes back.

Window: starts windowed at 1280x720 (fits smaller desktops). Alt+Enter switches
to desktop-resolution fullscreen and back. Close the window or use menu quit.
High-DPI awareness is enabled. To recover a bad saved display setting, close
the game and copy gamedata/config/sys_config.json.default over sys_config.json.

Default player 1 controls:
  Arrows: forward/run, backward, turn; in a car: gas, reverse, steering
  Space: jump / handbrake; Left Ctrl: shoot
  Enter: enter / leave car; F: enter as passenger
  X / Z: next / previous weapon; Tab: special / horn; R: next radio station
Xbox-compatible gamepad (Gamepad1):
  Left stick / d-pad moves; right stick aims and fires on foot
  A: jump / handbrake; Y: enter / leave car; X: enter as passenger
  Right / left bumper: next / previous weapon
  Right trigger: shoot on foot / gas in car; left trigger: brake / reverse
  B: special / horn; left stick click: next radio station
Esc returns from gameplay to the menu. Bindings: gamedata/config/inputs.json.

Camera: F9 cycles classic (north-up), reperspective (travel heading up; default)
and experimental combat (aim direction up). F8 toggles dynamic look-ahead.
Config: "cam_mode": "classic", "reperspective" or "combat";
"cam_smoothing": "instant", "fast" (default) or "smooth".
"g_controls": "auto" (gamepad twin-stick), "classic", or "twinstick"
(keyboard arrows move in screen directions, mouse aims, Left Ctrl fires).

Preferences are saved to gamedata/config/sys_config.json on normal exit/menu
save; profiles/scores are saved to gamedata/PLAYER_A.DAT. Back up that file.
Original GTADATA/PLAYER_A.DAT is only read for import, never overwritten.
Close the game before editing config. Keep the entire extracted folder writable;
avoid Program Files, protected folders, and running directly inside the zip.

Early Carnage3D remake prototype. Built and statically checked on Linux only;
Windows launch, graphics, sound, controls, persistence and gameplay still need
owner testing. The Android scripted-input test hook is disabled in this build.

Controllers (USB or Bluetooth):
Pair/connect the controller in Windows first; it can also be connected while the
game is running. The first controller found becomes Gamepad1. Layouts come from
gamecontrollerdb.txt next to the exe; a controller not listed there is read with
a generic layout. The log lists every controller found ("Gamepad1: name [id]").
If buttons are wrong, send that log line.
