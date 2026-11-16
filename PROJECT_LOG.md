# Project Log

## 2026-11-16 — Game files acquired and inspected

### Source (authorized)

The archive.org redump item (`gta-pc-redump`) is access-restricted (HTTP 401
from the storage node), so it was not used.

Instead, the files come from Rockstar's **official 2002 freeware re-release**
("Rockstar Classics"), mirrored at
<https://archive.org/details/gta_rockstar-classics>. Rockstar distributed this
build for free, so it's an authorized source. It's the Windows build Rockstar
patched for modern PCs (2002) and doesn't include the London mission packs.

Note: this is the freeware build, not the redump "(USA) (Rev 1)" CD image.
Game data (maps, styles, missions) is the same game. The executable is
Rockstar's 2002 Windows build.

### Files here

| Path | Contents |
|---|---|
| `original/GTAINSTALLER.exe` | Pristine download. Keep it untouched. |
| `original/GTAINSTALLER.exe.sha256` | Recorded hash |
| `installer-unpacked/GTAINSTALLER/` | Self-extracting zip contents (InstallShield 5 cabs) |
| `source-assets/Program_Executable_Files/` | Extracted game (pristine reference copy, don't modify) |

`GTAINSTALLER.exe` — 344,338,033 bytes

- MD5    `def8d7c8635e66f9fc2dd585aa5761ab` (matches archive.org)
- SHA-1  `762e03126a226e92e2f6827ca0750489595b52b2` (matches archive.org)
- SHA-256 `2102d4143f09dfa0c7022da1cdf41054ec8c04126d5557b9eb3050ad19879a7f`

`WINO/Grand Theft Auto.exe` — 774,144 bytes,
SHA-256 `6a5c313d9c0d87b4efa527baac7097dd4a149b9b2da5261abb224083f5cdc1c2`

Extraction: 7-Zip for the outer zip, then `unshield` 1.5.1 (built from source in
`~/src/unshield` and installed to `~/.local/bin`, because sudo is blocked) for
`data1.cab`. 194 of 195 files were extracted. **`Music/Track8.wav` failed** because
it spans `data1.cab` → `data2.cab`, which unshield mishandles. It's one music
track and doesn't matter for the camera work. A Windows extractor or running
the installer under Wine can recover it later.

Key data: `GTADATA/NYC.CMP`, `SANB.CMP`, `MIAMI.CMP` (city maps),
`STYLE00{1,2,3}.G24/.GRY` (tiles, sprites, car/block geometry), `MISSION.INI`.

### Executable findings (`Grand Theft Auto.exe`)

- PE32 i386 GUI, image base `0x400000`, `.text` ≈ 660 KB, no packing seen.
- Static imports: `mss32` (Miles audio), `smackw32` (Smacker video), `DPLAYX`
  (multiplayer), and the usual Win32 DLLs. There's **no static `ddraw.dll` import**.
  `ddraw.dll`/`DirectDrawCreate` are loaded dynamically.
- Built on **SciTech MGL** (strings: `MGLDirectDrawWindow`, `UniVBE`, VESA 2.0).
- Strings reference pluggable render drivers `DDRAW8/16/24/32.DRV` and
  `3DFX.DRV`, but none ship in this build, so they're probably linked in.
  Needs confirming during disassembly.
- Camera-related strings: `lozoom`, `hizoom`, `vhizoom` (zoom levels only, no
  camera rotation). Sprite/object rotation strings exist.

**Implication:** GTA 1's city is made of real 3D blocks drawn with
perspective, and sprites already rotate. So a yaw-rotated camera works
mathematically, but the original software renderer almost certainly assumes
an axis-aligned view. Tile-rectangle culling, face selection/ordering, and the
quad texture mapper would all need patching inside the exe.

## Open decision: implementation path (needs user approval)

1. **Patch the original executable.** Highest fidelity. You disassemble
   (Ghidra) the camera/projection, culling and block-face rasterizer, then hook
   or rewrite them to apply R(-θ). This is a large reverse-engineering job,
   with the main risk in the software rasterizer.
2. **Use an open-source GTA 1 engine** that loads these original data files,
   for example Carnage3D (github.com/codenamecpp/carnage3d, C++, OpenGL, MIT).
   Here camera rotation is a view-matrix change, so it's the fastest way to a
   playable prototype. The catch is that these engines are incomplete
   reimplementations, which conflicts with "preserve original gameplay".
3. **Hybrid.** Prototype the camera feel in an open-source engine first, then
   port the proven transform into the original exe.

Also still open: on-foot control scheme (Option A vs B) and camera anchor position.


## 2026-11-16 — Session 2: re-verification + first Ghidra pass

- Re-verified `original/GTAINSTALLER.exe` against its .sha256: **OK**. Nothing in
  `original/` or `source-assets/` was modified.
- Tooling on codebox: Ghidra 12.2 (`~/.local/bin/analyzeHeadless`), Java 21,
  gcc/cmake/git present. **Missing:** Wine, DOSBox, SDL2/GLEW/GLFW/OpenAL dev
  packages (sudo blocked, so none were installed). This means the original exe
  can't be run on codebox yet; testing it needs Windows (e.g. PowerStorm) or Wine.
- Carnage3D repo is reachable from codebox (`git ls-remote` OK, HEAD 3dc03b2b).

### New layout
| Path | Contents |
|---|---|
| `re/bin/gta.exe` | Working copy of `Grand Theft Auto.exe` (same SHA-256 6a5c313d…) |
| `re/ghidra-project/` | Ghidra project `gta`, auto-analysed (2041 functions) |
| `re/scripts/CameraRecon.java` | Headless post-script: string xrefs + decompile |
| `re/camera_recon.txt` | Output of that script |

### Findings
- `FUN_00415310` = video-mode setup. It calls MGL_registerDriver (`FUN_0048aee0`)
  for DDRAW8/16/32 and PACK8/16/32. These are **SciTech MGL pixel-format
  framebuffer drivers linked into the exe**, not 3D APIs. It picks a 640x480
  mode by default (`0x280 x 0x1e0`) in 15/16/32 bpp.
- **Conclusion:** GTA 1 does all its own projection and rasterization in software
  and writes pixels into an MGL surface. There's no 3D API to intercept, so a
  wrapper/compat-layer approach (like hooking D3D) can't rotate the view. The
  camera rotation has to go into GTA's own render code (path 1) or into a
  reimplementation (path 2).
- `FUN_00422b90` is the error-string table/dispatcher, and `FUN_00422900`
  raises errors by code. Its callers (codes for "invalid rotation in sprite",
  "invalid block for face access") are the next entry points for finding the
  sprite and block-face renderers.
- `lozoom/hizoom/vhizoom` strings have no direct code xrefs (probably used via a
  table), so the next pass should follow data xrefs to the zoom table to
  find the camera struct.

## 2026-11-16 — Decision + working prototype (Carnage3D, hybrid phase A)

**Decision (user):** Hybrid path. Prototype in Carnage3D first, then port the proven
camera into the original exe.

**Design defaults chosen (no further sign-off needed, easy to change):**
- On-foot controls: **Option A**. GTA 1's own keyboard controls are already
  heading-relative (Up = forward, Left/Right = rotate), so Option A *is* the
  original scheme and no control changes are needed.
- Camera anchor: player centred, with the original car look-ahead kept.

### Build (no root)
- Dev deps fetched with `apt-get download` and unpacked with `dpkg -x` into
  `~/.local/c3d-deps/root` (GLFW, GLEW, OpenAL, GLM, GL/X11 headers).
- Source `engines/carnage3d` (upstream 3dc03b2 + Box2D submodule), build dir
  `engines/carnage3d-build` (CMake Release, `-Wno-error`).
- Patch: `engines/perspective-lock.patch`
  (sha256 b7da117e…, 3 files, +115/−17).

### What the patch does
- `FollowCameraController`: the camera's screen-up vector = the player's world
  heading (`(cos θ, 0, sin θ)`), or the car's heading while driving. Only the
  view matrix changes, so physics, AI, collisions and world coordinates are
  untouched. Shortest-arc smoothing at 12/s. **F9** toggles lock on/off.
  `C3D_LOCK=0` starts with it off; `C3D_TEST_SPIN=<deg/s>` is a test-only spin.
- `GameCamera::ComputeViewBounds2`: visible-area box is now the rotated view's
  bounding circle, so no blocks/cars get culled at rotated screen corners.

### Run
`prototype/` = runnable copy: `bin/carnage3d`, `gamedata/` (engine data +
`config/sys_config.json` → `engines/gamedata-working/GTADATA`, a copy;
`source-assets` untouched).

    cd /mnt/T7/GTA-Classic-Perspective-Lock/prototype/bin
    LD_LIBRARY_PATH=~/.local/c3d-deps/root/usr/lib/x86_64-linux-gnu ./carnage3d

### Verified
- Runs at 60 FPS on codebox (Radeon, GL 4.6) with the original GTA 1 city data.
- `prototype/verify_lock.sh` → `shots/strip_lock_on.png`: the world rotates
  ~45°/s while the player sprite keeps the same on-screen orientation (facing up).
  `shots/strip_lock_off.png`: the world is fixed and the player spins (control).
- Not yet tested by hand: real keyboard play, driving, car entry/exit
  transitions. These need someone at the keyboard (codebox's display is
  720x1760 portrait, so the 1024x768 window is clipped; set `r_screenDims` to fit).

### Next (phase B — original exe)
Follow `FUN_00422900` error-code callers to the sprite/block-face renderers,
find the zoom table → camera struct, then locate the world→screen transform and
the tile-rectangle culling, and inject R(−θ). Testing the exe needs Windows
(PowerStorm) or Wine.
