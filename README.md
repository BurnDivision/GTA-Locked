# GTA Classic Perspective Lock

Make the original 1997 PC **Grand Theft Auto** play with a player-relative,
heading-locked camera: the player (on foot or in a car) always faces the top of
the screen and the world rotates around them. Only the camera changes. Maps,
physics, AI, collisions and missions are untouched.

    ScreenPosition = R(-θ) × (WorldPosition − PlayerPosition)

This repo holds **code, patches, scripts and notes only**. No game files.
The game data and working builds live on codebox at
`/mnt/T7/GTA-Classic-Perspective-Lock/` (see [PROJECT_LOG.md](PROJECT_LOG.md)).

## Status

| Phase | State |
|---|---|
| 1. Acquire + verify game files | Done (Rockstar 2002 freeware release; hashes recorded) |
| 2. Technical investigation | Done for path choice. Original exe = SciTech MGL software renderer, no 3D API to hook |
| Path chosen | **Hybrid**: prototype in Carnage3D, then port into the original exe |
| 3. Perspective-lock prototype (Carnage3D) | **Working**, verified by screenshots (world rotates, player stays facing up) |
| 4. Vehicle integration | Coded (camera follows car heading while driving); not yet hand-tested |
| Phase B: port into original `Grand Theft Auto.exe` | Not started. Ghidra project prepared |
| **Android app** | **Working.** Release APK 0.3.0 (arm64 + x86_64). Tested on the emulator: the original cities load, the touch controls drive the player, and the world rotates around them |
| **Aspect ratios** | **Auto, 16:9, 4:3 or fill**, with black bars as needed. Auto picks 4:3 on a foldable's unfolded screen and 16:9 when folded. Works on Android and desktop |

## Layout

| Path | What |
|---|---|
| `prototype/carnage3d/perspective-lock.patch` | Camera patch for Carnage3D upstream `3dc03b2` (desktop) |
| `prototype/carnage3d/perspective-lock-android.patch` | Cumulative patch: camera + Android port (use this one) |
| `android/` | Android app (Gradle/NDK), `setup_deps.sh`, `emu_test.sh` |
| `prototype/verify_lock.sh` | Spins the player and screenshots lock ON vs OFF |
| `re/scripts/CameraRecon.java` | Ghidra headless script: render/camera string xrefs + decompile |
| `PROJECT_LOG.md` | Full history, hashes, findings, decisions |

## Build the prototype (Linux, no root)

```sh
git clone https://github.com/codenamecpp/carnage3d && cd carnage3d
git checkout 3dc03b2 && git submodule update --init
git apply /path/to/prototype/carnage3d/perspective-lock.patch
# deps: GLFW, GLEW, OpenAL, GLM dev packages (or apt-get download + dpkg -x into a prefix)
cmake -B ../build -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_FLAGS=-Wno-error && make -C ../build -j
```

Set `g_gtadata` in `gamedata/config/sys_config.json` to your `GTADATA` folder.

## Android app

`android/` is a Gradle/NDK project. It builds the same patched engine into an APK.

* **Platform layer:** `src/android/glfw_sdl_shim.*` implements the GLFW calls the engine
  uses, on top of SDL2. It also adds touch controls and gamepad support.
* **Rendering:** OpenGL ES 3.0, using the engine's existing ES path. Audio is OpenAL Soft.
* **Game data:** the APK does **not** contain GTA data. On first launch, tap
  **Import GTA data folder** and pick your `GTADATA` folder (or the game folder that holds it).
  It's copied into the app's storage.
* **Requirements:** Android 8.0+ (API 26), GLES 3.0. Landscape only.

Touch layout:
* **Left thumb pad:** Up = forward, Down = back, Left/Right = turn.
* **FIRE**, **CAR** (enter/exit), **JUMP** (handbrake in a car), **WPN** (next weapon),
  **HORN**, **LOCK** (toggle the perspective lock).

A connected gamepad works too, and hides the touch overlay.

Aspect ratio:
* **Auto** (Android default) picks the nearer of 4:3 and 16:9 to the screen shape. On a
  Galaxy Z Fold that means 4:3 on the unfolded inner screen (≈1.1:1) and 16:9 on the folded
  cover screen (≈21:9). It switches live when you fold or unfold.
* **16:9** or **4:3** can be chosen directly. The game view is centred, with black bars
  wherever the screen shape differs.
* **Fill** uses the whole screen.
* Choose it on the launcher screen, or switch in game with the small ratio button next to
  LOCK (shows "AUTO", "16:9", "4:3" or "FILL").
* When the view has bars above and below (as on the unfolded Fold), LOCK and the ratio
  button move into the top bar so they don't cover the HUD.
* Setting: `r_aspect` in `config/sys_config.json`, saved when changed.

Build:
```sh
cd android
./setup_deps.sh                # clones carnage3d (+ applies patch), SDL2, OpenAL Soft, GLM
./gradlew assembleRelease      # needs Android SDK 35, NDK 27.2, CMake 3.22.1
```

## Controls

* Original GTA 1 controls: Up = forward, Left/Right = turn. They're already
  heading-relative, so they match the locked camera with no changes (on-foot "Option A").
* **F9**: toggle perspective lock on/off (for comparison).
* **F7**: cycle aspect ratio auto → 16:9 → 4:3 → fill (`r_aspect`; desktop default is `fill`).
* `C3D_LOCK=0`: start with the lock off. `C3D_TEST_SPIN=<deg/s>`: test-only auto-turn.
