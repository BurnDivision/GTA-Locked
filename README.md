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

## Layout

| Path | What |
|---|---|
| `prototype/carnage3d/perspective-lock.patch` | Camera patch for Carnage3D upstream `3dc03b2` |
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

## Controls

* Original GTA 1 controls: Up = forward, Left/Right = turn. They're already
  heading-relative, so they match the locked camera with no changes (on-foot "Option A").
* **F9**: toggle perspective lock on/off (for comparison).
* `C3D_LOCK=0`: start with the lock off. `C3D_TEST_SPIN=<deg/s>`: test-only auto-turn.
