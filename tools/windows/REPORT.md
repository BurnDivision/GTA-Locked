# Windows x86_64 build hardening report (TASK2)

2026-11-18. Package: `dist/GTA-Perspective-Lock-windows-0.6.9.zip`.
Version is read from `android/app/build.gradle`; Windows file/product version
is 0.6.9.0. Verification is static/host-native only: no Windows PE was executed.

## Reproduce and evidence

```
./build.sh > rebuild2-final.log 2>&1
./package.sh > package2-final.log 2>&1
python3 verify.py > verification2.log 2>&1
bash -n build.sh package.sh
sha256sum -c SHA256SUMS
```

Build, package and verification exited 0. The final build re-synced from the
authoritative engine, applied the cumulative patch at zero fuzz, compiled
Release, compiled the Windows resource and linked at 100%. Packaging recursively
checked DLL dependencies and tested ZIP CRCs before atomically replacing the ZIP.
`file-type.txt`, `dll-imports.txt`, `zip-listing.txt`, `SHA256SUMS` and
`verification2.log` describe the final artifacts. Logs for the startup,
window/save and resource groups are retained; each group was built and packaged.
The earlier pass's `rebuild.log`, `build.log` and `configure.log` remain available.

All edits and test/build output stayed in windows-build. The actual engine tree,
Android tree, original game files and saves were not edited. No Wine, sudo,
network, package installs or remount were used. This folder is not a Git checkout;
no commit was created. The T7 reports a read-only mount, but the supplied workspace
permits scoped writes here; no attempt was made to bypass its boundary.

## Deliverables

- `engine.patch`: one cumulative patch against `engines/carnage3d`, including
  the earlier MinGW fixes. Windows behavior is guarded by `_WIN32` or `MINGW`.
  Other platforms retain their original branches; Linux/Android were not rebuilt.
- `build.sh`, `toolchain-mingw.cmake`: reproducible source refresh + zero-fuzz
  patch, x64 Release, C++17, static compiler runtime/GLEW/GLFW, version taken
  from Android. New resources are carried in the patch and survive source refresh.
- `package.sh`: GUI exe, OpenAL DLL, engine data, Windows defaults, README and
  licenses; strips test-input/profile residue from the staging copy, validates
  imports/CRCs, refreshes file type and checksums.
- `regenerate-patch.py`: regenerates the cumulative patch, preserving mixed CRLF
  and files with no final newline. Skips patch backup/reject artifacts. Review
  the patch before a build, since build.sh intentionally replaces the engine copy.
- `verify.py`, `tests/profile-store.cpp`, `tests/win32-shim/windows.h`: repeatable
  artifact, patch replay, GLSL and profile-persistence checks.
- `README-windows.txt`, `HANDOFF2.md`: practical install/controls/recovery and
  the owner's first checks. README is copied into the ZIP as README.txt.

## Startup audit: failure paths, fixes and limits

1. **Before main / loader:** exe is PE32+ AMD64, Windows GUI subsystem (no console),
   Windows 10/11 target. OpenAL32.dll must be extracted alongside it even if audio
   is disabled in config. Missing DLLs, damaged ZIP extraction, OS loader refusal
   or antivirus blocking occur before engine diagnostics; Windows owns those
   dialogs. The package import checker finds no unresolved non-system imports.
2. **Executable and working paths:** Main uses GetModuleFileNameW, requires a
   short ASCII executable path and gives a wide-character dialog if unsupported.
   It changes the process working directory to the exe's directory before any
   config/data reads. This also stabilizes relative external-data and MUSIC paths
   when launched through a shortcut with another working directory. Legacy narrow
   data readers remain; arbitrary Unicode and long paths are explicitly unsupported.
3. **Console/log:** Console opens `GTA-Perspective-Lock.log` beside the exe and
   mirrors every console message, including debug and shader diagnostics, flushing
   each message. Each launch starts a fresh log. An unwritable log path gives an
   actionable dialog and exits. Logs are not rotated and can grow during long runs.
4. **Engine gamedata:** filesystem initialization validates the adjacent gamedata
   folder; its exact expected path appears in the fatal dialog if missing. Engine
   shaders/config/fonts/entities stay distinct from the user's original GTADATA.
5. **Config/startup parameters:** load adjacent gamedata/config/sys_config.json,
   then apply command-line settings. Empty g_gtadata defaults to adjacent GTADATA;
   `-gtadata` wins over config. Relative overrides resolve from the exe directory.
   Invalid/missing JSON is logged and defaults remain usable. Config reads are
   anchored to gamedata, matching the write location.
6. **GTADATA setup:** Windows validates data location and map availability before
   creating the graphics window. Failure shows the expected absolute GTADATA path
   plus the log path. Map scanning accepts .CMP and .cmp. Ordinary Windows lookup
   handles mixed-case MISSION.INI/style/font/music names; specially case-sensitive
   Windows directories remain unsupported. No Linux/Android lookup rules changed.
7. **Memory/subsystem init:** reported filesystem, memory, graphics, GUI and game
   failures reach System::Terminate, which shows recent concrete diagnostics and
   the full log path. Windows fatal shutdown uses ExitProcess rather than trying
   to deinitialize partially initialized subsystems or save configuration. C++
   exceptions from the run path are logged and reported; access violations,
   exhausted memory and OS/driver crashes are not guaranteed to reach this dialog.
8. **GLFW/OpenGL:** Windows now requests OpenGL 3.3 forward-compatible core,
   matching the existing `#version 330 core` shader preamble. GLEW remains
   experimental/core aware and checks 3.3 availability. GLFW's actual driver/context
   error is logged before the generic init error and included in the fatal dialog.
   Microsoft Basic Display Adapter/OpenGL 1.1 is insufficient; vendor driver needed.
9. **Shader loading:** all six RenderProgram initialization results now propagate
   on Windows. Missing shader source, compile or link failure causes a reason/log
   dialog instead of continuing with unusable programs. Existing compiler/linker
   diagnostic strings are preserved. Desktop stdafx selects GLEW/OpenGL; C3D_GLES
   is only defined for Android/Emscripten. No GLES definition appears in this build.
   GLSL 330 vertex/fragment compilation and linking passed for all six programs;
   this does not establish driver execution, GPU limits or visual correctness.
10. **ImGui:** the original front-end draws using ImGui, so Windows treats its
    initialization/font-texture allocation failure as fatal. The earlier code
    considered it optional debug UI. The shipped TTF font is present; upstream
    ImGui also supplies a default font if an external font cannot be added.
11. **AudioDevice/AudioManager:** open-device/context errors are already logged.
    Windows skips AudioManager initialization without an initialized device;
    partial manager resource failure is cleaned up before disabling the device.
    The frame loop already skips audio updates without a device. No audio-device
    failure is promoted to fatal. OpenAL DLL loading still occurs before main.
    Sound-bank/music decoding failures are logged; missing music is nonfatal.
12. **CarnageGame/texts/menu:** required edition-specific FXT load failure is
    fatal on Windows. Menu font/image failures identify missing/invalid filenames;
    missing MISSION.INI is explicit. All six catalog sections (1, 2, 102, 103,
    202, 203) must resolve to scanned maps. Failure reports the configured data
    folder and exits visibly, fixing the previous silent first-frame quit.
    This front-end expects full GTA1 data; the old upstream demo/London claims
    do not describe the current chapter menu. No placeholder/original art is shipped.
13. **Scenario selection:** .CMP headers select their STYLE*.G24 file through the
    existing edition logic. Missing/corrupt map or style already returns false
    with loader diagnostics; Windows now also shows a chapter-load error/log path
    and returns to the menu. Binary parsers and world loading were not rewritten
    or fuzzed; corrupt original data can expose existing engine defects.
14. **MUSIC/AUDIO:** code requests MUSIC/Track1.wav through Track10.wav; this works
    beneath GTADATA or beside the exe (now stable working directory). GTADATA/AUDIO
    retains the VOCALCOM/LEVELxxx bank layout. WAVs are optional; unsupported or
    missing tracks are logged. No conversion, guessing, fetching or redistribution.
15. **Window/fullscreen:** packaged defaults and Windows fallback use 1280x720
    windowed, clamped to smaller desktops. Alt+Enter toggles desktop-native
    resolution fullscreen and restores window position/size. Invalid saved sizes
    are clamped. The default template resets troublesome display settings.
16. **DPI:** embedded manifest requests per-monitor v2 awareness, Windows 10
    compatibility and asInvoker execution. Framebuffer dimensions update viewport,
    scissor and player layouts after fullscreen/DPI changes. Mouse positions are
    converted into framebuffer coordinates for menu hit tests. This has not been
    checked across mixed-DPI monitors, taskbars, minimizing or display hotplug.
17. **Keyboard/mouse menu:** traced GLFW events through InputsManager to the
    front-end. Arrows/Enter/Esc cover all pages; Delete/name characters/Backspace
    cover rename; R opens the separate reset confirmation. Left-click covers menu
    rows, profile/city selection and bottom navigation; name entry uses keyboard.
    There is no touch-only step. Actual focus/hit targets remain Windows-untested.
18. **Gamepad:** GLFW's mapped gamepad state populates the same six analog axes
    and button events used by Android's shim and HumanPlayer::ProcessTwinStick /
    ProcessGamepadDriving. Config assigns player 1 to Gamepad1. Windows hotplug
    only marks mapped controllers present and clears stale axes on disconnect.
    Button polling uses GLFW_GAMEPAD_BUTTON_LAST. Menu uses d-pad/A/Start/B;
    gameplay bindings are copied from inputs.json.default and documented. Actual
    XInput recognition, dead zones, hotplug/focus and simultaneous inputs need testing.
19. **Save/config persistence:** app profiles live at gamedata/PLAYER_A.DAT;
    the original GTADATA/PLAYER_A.DAT is imported read-only when no app save exists.
    The original Windows CRT rename could not replace an existing save. Windows
    now closes/flushes the temp and uses MoveFileExA(REPLACE_EXISTING|WRITE_THROUGH),
    preserving the old save on failed replacement. Invalid existing profiles stay
    protected by the original mProfileWritable guard. Config uses the same staged
    replacement pattern in gamedata/config/sys_config.json, saved on normal exit
    and explicit menu saves. Entire extracted folder must be writable; no admin
    elevation or Program Files virtualization. Concurrent instances and forced
    power-loss durability remain outside this single-writer design.
20. **Test hook:** Windows cannot bypass the menu using config/test_input.txt;
    HumanPlayer::UpdateTestScript returns immediately. Packaging strips test-input
    and app-profile files from staging and creates fresh config defaults. Android
    and Linux retain their existing hook behavior. Archive inspection confirms no
    enabled test script, user profile, GTADATA or original asset files.
21. **Resources:** llvm-mingw windres embeds version metadata and the DPI manifest.
    No custom icon was added (optional). No runtime compiler/GLFW/GLEW DLL needed.

## Validation results

- Executable: `PE32+ executable for MS Windows 6.00 (GUI), x86-64, 14 sections`.
  PE header version is not an assertion of old-Windows compatibility.
- Import inspection: only OpenAL32.dll is a non-system dependency. Exe imports
  OpenGL32, WinMM, Kernel32, User32, GDI32, Shell32 and Universal CRT API sets.
  OpenAL imports Kernel32, msvcrt, ole32, Shell32, User32 and WinMM.
  No libc++, libunwind, winpthread, GLFW, GLEW or Android/GLES runtime DLL imports.
- PE resource parsing verifies actual embedded 0.6.9.0 file version, product name,
  DPI manifest and asInvoker execution level; checks GUI subsystem and AMD64.
- ZIP: 27 files; CRCs pass; archived exe, DLL and README match their final inputs;
  active config files match packaged templates; windowed 1280x720 defaults;
  no original assets/test script/profile. SHA256SUMS verifies exe and ZIP.
- Patch replay: 18 changed/new engine files apply with zero fuzz, reproducing
  reviewed source bytes exactly, including mixed line endings and Main.cpp's
  absent final newline. No .orig/.rej artifacts. A regression check first caught
  the missing-newline generator issue, which is fixed in regenerate-patch.py.
- Profile tests: verified the original header fails the second save under a
  Windows CRT rename characterization. Patched tests pass repeated save/load,
  replacement content, missing-file status, failed replacement preservation and
  temp cleanup. Tests run natively and with a Linux-hosted Windows API shim;
  MoveFileExA behavior on actual Windows/filesystems is still untested.
- All six GLSL 330 shader pairs compile/link using glslangValidator.
  Shell scripts pass bash -n; Python helpers compile. No engine-wide test runner
  exists in the copied tree; host checks are the focused suite for this task.
- Four existing compiler warnings remain: two incomplete alignment switches in
  HUD.cpp, std::iterator deprecation in intrusive_list.h, and an incomplete
  damage-cause switch in Pedestrian.cpp. Bundled Box2D has a CMake minimum-version
  deprecation warning. No warnings were suppressed; no new compiler warning added.

## Risks that remain untested (estimated likelihood, highest first)

1. Original data edition/completeness: front-end assets, six MISSION.INI chapters,
   corresponding maps/styles and FXT vary between distributions. First check the
   missing-data dialog, then a full retail GTADATA and menu-to-Liberty-City launch.
2. Driver rendering: actual Intel/AMD/NVIDIA context creation, GPU allocations,
   core-profile drawing, palette/sprite rendering and sustained frame delivery.
3. Input/focus: XInput mapping, hotplug/reconnect, twin-stick/fire/vehicle controls,
   keyboard/mouse menu actions and input after Alt+Tab or modal dialogs.
4. DPI/window transitions: 125-200% scaling, mixed monitors, Alt+Enter in menu and
   gameplay, restored/fullscreen startup, minimize/restore and display hotplug.
5. Audio: installed endpoint availability, silent fallback, bank/WAV formats,
   music streaming and changing/disconnecting playback devices during a session.
6. Save persistence: actual MoveFileEx replacement and relaunch restoration,
   readonly/locked files, antivirus interference, disk-full, concurrent processes
   and forced interruption. Back up profiles before testing.
7. Distribution/loader: unsigned-exe reputation/SmartScreen, Defender, DLL loading
   and system API availability on the owner's Windows installation. No installer,
   signing, administrator access or additional redistributable is assumed.

Known documented limits: short ASCII paths, ordinary case-insensitive Windows
folders, complete GTA1 data, writable portable extraction. Not runtime-verified:
any Windows launch, dialogs, rendering, audio, persistence or gameplay. Next safe
task is the owner checklist in HANDOFF2.md; no build/packaging blocker remains.
