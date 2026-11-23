# Reperspective camera and twin-stick controls: design notes

Scope: GTA 1 proof of concept on the Carnage3D engine (desktop and Android). World coordinates,
physics, collision, AI, weapons and projectiles stay authoritative and unchanged. The camera is a
view transform only, and the HUD and touch overlay are drawn in screen space (they never rotate).

## Camera modes (`cam_mode`, F9 or the LOCK touch button cycles them; label NORTH / LOCK / AIM)

| Mode | On foot | In a car |
|---|---|---|
| `classic` | Original north-up view, no rotation | Same |
| `reperspective` (default) | Tank controls: the body heading points up. Twin-stick: a *persistent travel heading* points up; the view holds while the player aims independently | The car's actual heading points up |
| `combat` (experimental) | The view follows the aim. Right stick x turns the aim; the left stick moves relative to the view | The car's heading |

Four directions are kept separate:
- **Movement:** `PedestrianCtlState::mMoveDirection`, the actual direction of travel.
- **Facing:** the body orientation.
- **Aim:** the twin-stick aim direction.
- **Camera heading:** `FollowCameraController::mCameraHeading`.

### Persistent travel heading (`reperspective::TravelHeadingTracker`)
- **Standing still or creeping** (below 0.6 m/s) holds the heading, so the view doesn't wobble while stationary.
- **Small adjustments** (under about 11°) are followed slowly, with a 0.8 s time constant.
- **Larger changes** start the view following (0.12 s time constant) once the travel direction has
  stayed on one side of the heading for 0.25 s. This covers sharp turns and steadily curving runs.
  Rapid side-to-side input keeps switching sides, so it never commits.
- **While aiming:** the view holds when the right stick is deflected, and for 0.75 s after firing.
  Strafing and backpedalling therefore don't rotate the world.

### Latched input frame (`reperspective::InputFrameLatch`)
In Reperspective twin-stick play the view turns towards the travel direction. If stick directions
always used the *current* screen orientation, a held "right" would keep turning the player and the
view in an endless circle: the closed-loop unit test turns 2943° in 6 s.

While a stick is held, its screen-to-world mapping is fixed at the moment it was pushed. The world
movement direction then stays constant while the view catches up. The mapping is taken again when the
stick is released or turned more than about 35°.

Trade-off: after the view has turned, a held stick may point slightly differently on screen from where
the player now walks. Releasing or turning the stick re-aligns it.

### Smoothing (`cam_smoothing`)
- **Interpolation:** frame-rate independent, `1 - exp(-dt/τ)`, along the shortest arc. Wrap-around at ±180° is safe.
- **Presets:** values are settle times, meaning 90% of a step change:
  - Instant: 0 ms;
  - Fast (default): 125 ms;
  - Smooth: 250 ms.
- **Dynamic camera** (`cam_dynamic`, F8): adds up to 0.12 s of time constant at speed, and looks ahead
  along the body heading so the player sits lower on screen when running or driving.
- **Rate limits:**
  - In a car: 120°/s at walking pace, rising to 400°/s at 8 m/s, so collisions and low-speed physics
    wobble don't shake the view.
  - On foot while following the travel heading: 240°/s.

## Controls (`g_controls`: `auto`, `classic` or `twinstick`)
- `auto` (default): twin-stick on foot whenever the gamepad was the last input used. Keyboard and touch
  use the original tank controls.
- `twinstick`: twin-stick on foot for the keyboard too.
  - Keyboard: the movement keys move in screen directions.
  - Mouse: aims, when `mouse_aiming` is enabled. Fire with the left button or the fire key.
- `classic`: the original controls everywhere.
- **Gamepad, on foot:**
  - Left stick or d-pad: move in a screen direction. Walk below 70% deflection, run above.
  - Right stick: aim.
  - Firing: automatic past `g_autoFireThreshold` (default 0.6) when `g_autoFire` is 1 (default). The right
    trigger always fires.
- **Gamepad, in a car:** the left stick steers in proportion, the right trigger is the gas, and the left
  trigger brakes or reverses. The right stick also drives: up is the gas and down brakes, then reverses.
  It takes priority over the triggers outside its dead zone (`src/DrivingInput.h`).

### Constraints kept from the original game
- The weapon fires along the character's facing, as in the original, so facing follows the aim whenever
  the player aims. Movement is independent: the engine gained `mUseMoveDirection` so a pedestrian can
  walk one way while facing another. Weapon mechanics and projectiles are untouched.
- Walking with fists can't attack (original engine rule); kept.
- Movement and run speeds are the original values.

## Input focus (audit finding: analog input bypassed UI capture)
Sticks produce no input events, so their motion re-enables input processing. That now happens only
while the player controller has the input focus (`mInputFocusLost`). Nothing is processed while an
ImGui window (console, cheats) is capturing the keyboard.

## Modules
- `src/reperspective/CameraHeading.h`: wrap, shortest-arc smoothing, presets, travel heading tracker.
- `src/reperspective/TwinStick.h`: screen basis, dead zones, input latch, move/face/aim evaluation, combat turn.

Both are header-only, plain C++ with no engine types, so they can be reused in another engine (e.g. GTA 2)
later. The engine adapters live in `FollowCameraController` (camera) and `HumanPlayer` (input).

## Tests
- **Native unit tests:** `prototype/carnage3d/tests/run_tests.sh <carnage3d/src>`. They cover:
  - wrap and shortest arc;
  - frame-rate independence (40 / 80 / 160 fps);
  - instant and rate-limited smoothing;
  - travel tracker: stationary, rapid changes, curving runs, 180° crossing;
  - screen basis, dead zones, latch, strafing, backward fire, auto/manual fire, combat turn;
  - the closed-loop "held stick" test.
- **Runtime matrix on the emulator:** a scripted gamepad plus telemetry. Implementation:
  - `HumanPlayer`, test-only, active only when `config/test_input.txt` exists;
  - `tools/rp_tests/{matrix.txt, run_matrix.sh, analyze.py}` on the T7.

  It covers:
  - Classic with twin-stick;
  - Reperspective with twin-stick: held stick, strafe and fire, backward and fire, stationary aim,
    rapid changes, a full 360° sweep through ±180°;
  - Combat;
  - driving, including right-stick gas and reverse (distance moved along the car's heading).

  It checks world-space movement, actual velocity, facing, projectile headings and camera steps per frame.
- **Not covered:**
  - Different frame rates at runtime: the emulator runs at one rate; the unit tests cover dt independence.
  - Different aspect ratios with these modes: the camera code is aspect independent.
  - Touch twin-stick: one emulator run (single-finger swipes; adb can't do two fingers at once).

## Open trade-offs and remaining work
- Touch twin-stick: in a car the aim stick is the throttle (up = gas, down = brake/reverse). A sideways-only push
  of the aim stick still counts as gas, because the touch layer also reports it as the fire trigger.
- Combat mode is experimental:
  - gamepad only;
  - auto-fire is off in this mode because the right stick turns the view;
  - mouse aiming isn't supported because it would spin the view.
- A right-stick-only movement after a UI window closes doesn't regain the input focus until a button
  or the left stick is used.
- Real-controller feel (dead zones, thresholds, smoothing) still needs tuning on a device.
