# Last Epoch × Survivors-like — Implementation Guideline

Status: **planning only**. Nothing is built yet. This is how to approach turning
Last Epoch (LE) into a survivors-like (auto-attacking, horde survival, level-up
choices, timed runs) as a client-side mod.

---

## 1. What we're working with (checked on PowerStorm, 2026-11-18)

| Fact | Value | Why it matters |
|---|---|---|
| Install | `C:\Program Files (x86)\Steam\steamapps\common\Last Epoch` | Target for the mod loader |
| Engine | Unity **6000.4** (Unity 6.4) | Very new — tooling may lag |
| Scripting backend | **IL2CPP** (`GameAssembly.dll`, `global-metadata.dat`) | No plain C# DLLs; need MelonLoader + Il2CppInterop |
| Metadata version | **39** (`AF 1B B1 FA 27 00 00 00`) | MelonLoader/Cpp2IL must support v39 — **verify first** |
| Game code assemblies | `LE.dll`, `LE.Core.dll`, `LE.Networking.Core.dll`, `LE.PCG.dll`, `LE.Kos.dll`, `LE.SignalBus.dll`, `LE.UI.Controls.dll` | Where all the hooks will live |
| Notable libs | Rewired (input), DOTween, UniTask, FMOD, ImGui (`RG.ImGui`), Addressables | Reusable for UI/input/tweening |
| Last patched | 2026-11-18 (today) | Every patch can break hooks — see §6 |
| Also present | `Last Epoch CT` folder + signed Community Tester NDA | **Do not use the CT build for this mod** (see §7) |

---

## 2. Core principle: hijack LE's systems, don't rebuild them

LE already has most of what a survivors-like needs: dozens of skills with deep
specialization trees, a stat/affix system, hordes of enemy types, and
**a wave-based mode already (the Arena)**. The mod should be a thin *director*
on top of those systems, not a new game in a new engine.

The rule for every feature: **find the LE system that is closest, then
redirect it.** Write new systems only where LE has nothing (spawn
director, level-up choice screen, run timer, meta-progression).

---

## 3. Mechanic mapping

Treat each survivors mechanic as a translation into LE terms.

| Survivors mechanic | LE system to reuse | What the mod does |
|---|---|---|
| **Auto-attacking weapons** | Skill bar abilities + specialization trees | Each equipped skill fires itself on an interval at the nearest/best target. The player only moves. |
| **Movement-only control** | Player movement (use native WASD if the current build has it; otherwise drive the nav agent from input) | Hide or ignore click-to-attack; left click does nothing during a run |
| **Weapon level-ups** | Skill specialization points | "Level up skill X" = allocate a point in that skill's tree, picked from valid nodes |
| **Passive items** | Stats/affix modifiers | "+10% area", "+cooldown recovery", "+move speed" applied as runtime stat mods |
| **Weapon evolutions** | Tree capstone nodes / unique-item effects | Max a skill + own the matching passive, then a boss chest grants a capstone/unique effect |
| **Horde spawning** | Arena wave controller + enemy actor prefabs | Replace the Arena's wave logic with a *time-based* spawn director |
| **XP gems + magnet** | XP gain on kill | Hold XP back on kill and drop a pickup orb instead. Collecting it awards the XP, with a pickup radius stat |
| **Level-up choice (pick 1 of 3–4)** | None (new) | Pause the game and show cards: new skill / skill point / passive / gold |
| **Floor pickups (heal, magnet, bomb)** | Health potion pickups, ground items | Spawn from breakables/elites |
| **Treasure chests from elites** | Rare/exalted enemy deaths | Elite death drops a chest giving 1/3/5 upgrades |
| **Timed run + final reaper** | None (new) | 15 or 30 min clock; at the end spawn an unkillable finisher; surviving = win |
| **Characters** | 5 classes / 15 masteries | Each mastery = a character with a starting skill and a passive bonus |
| **Meta upgrades with gold** | None (new) | Gold persists in the mod's own save file and buys permanent run-start bonuses |
| **Stage select** | Arena + existing zones (via scene loading) | Start with the Arena only; add zones later |

### Auto-cast rules (the hardest and most important system)
- Use each skill's real cooldown where it has one. Skills without a cooldown
  (spammables) get a mod-assigned interval based on cast speed / mana cost.
- Targeting modes per skill: nearest enemy, densest cluster, facing
  direction, self-centred (auras/novas), random. Put these in a per-skill data table.
- Mana: either drop the mana cost during runs or regenerate it fast enough. Do not
  let auto-cast stall on mana, because that feels broken.
- Channelled and movement skills (e.g. charge or teleport skills) are poor
  weapons. Exclude them from the pool, or turn them into dash-on-keypress.
- LE's abilities are probably tied to animation and the input pipeline. Before
  writing auto-cast, **find out whether casting can be triggered from code**
  (an ability-use method on the player actor) or whether input has to be
  faked through Rewired. This decides the whole architecture.

---

## 4. Mod architecture

```
SurvivorsMod (MelonLoader mod DLL)
├── Adapter/        ← the ONLY code that touches LE types or Harmony patches
│   ├── PlayerAdapter      (position, stats, cast ability, grant spec point)
│   ├── EnemyAdapter       (spawn actor by id, kill events, despawn/recycle)
│   ├── ArenaAdapter       (take over / disable native wave logic)
│   ├── XpAdapter          (intercept XP gain)
│   └── UiAdapter          (pause, input lock)
├── Run/            ← run state machine: Lobby → Running → LevelUp(paused) → Victory/Death → Results
├── Systems/
│   ├── AutoCast           (intervals, targeting)
│   ├── SpawnDirector      (time curve, enemy pool, elites, bosses, finisher)
│   ├── Pickups            (XP orbs, magnet, heal, chests)
│   ├── Upgrades           (choice generation, weights, evolutions)
│   └── Meta               (gold, permanent upgrades, own JSON save)
├── UI/             ← timer, kill count, XP bar, level-up cards, results
└── Data/           ← JSON tuning: spawn curves, upgrade pool, per-skill auto-cast table
```

Rules:
1. **All LE-specific code goes in `Adapter/`.** When a patch renames something,
   only that folder changes.
2. **Look up types and methods by name, never by offset or RVA.** Fail loudly at
   startup (`[Adapter] PlayerAdapter: method X not found`) instead of
   crashing mid-run.
3. **All tuning lives in `Data/` JSON**, so balance passes don't need a rebuild.
4. **Systems only talk to Adapters through interfaces.** That means the spawn
   director and upgrade logic can be unit-tested outside the game.
5. **Run state is temporary.** Skill points, stat mods and level gained
   during a run must not leak into the real character's save.

---

## 5. Phased plan

Each phase ends with something you can play. Don't start a phase until the
previous one is fun or at least working.

### Phase 0: Toolchain and safety (before any code)
1. **Back up saves**: `%USERPROFILE%\AppData\LocalLow\Eleventh Hour Games\Last Epoch\Saves`.
2. Check that the current MelonLoader release supports **Unity 6000.4 / metadata v39**.
   If it doesn't, wait or use a nightly. This is the single biggest blocker risk.
3. Install MelonLoader (the installer is already in Downloads), launch once to
   generate the Il2Cpp interop assemblies, and confirm the game still runs.
4. Add **UnityExplorer** (an IL2CPP build matching the MelonLoader version) for
   live inspection: object hierarchy, components, calling methods at runtime.
5. Make a dedicated **offline** test character used only for the mod.
6. Set up a C# project that references the generated interop assemblies in
   `MelonLoader/Il2CppAssemblies`.

### Phase 1: Recon (read only, no gameplay changes)
Fill a `SYMBOL_MAP.md` with the real class and method names for:
- the player actor, its position, its stats component, its ability list
- the method that **uses an ability** (and whether it can be called from code)
- enemy spawning: how the Arena/zones instantiate enemies, and the enemy id/prefab list
- the death event (for kills, XP and drops) and the XP gain path
- the Arena wave controller
- skill specialization: how a point is allocated in code
- pausing (time scale versus the game's own pause) and how offline mode is detected

Use UnityExplorer live, plus the generated interop assemblies opened in a
decompiler. **Write nothing that changes behaviour in this phase.**

### Phase 2: Vertical slice (the fun test)
- One character, one skill, auto-cast to the nearest enemy
- Arena with a simple time-based spawn director (density ramps every 30 s)
- On-screen timer and kill counter
- **Success test:** walking around while the skill clears enemies feels good for 5 minutes.
- **Performance test:** find the enemy count where FPS drops. LE isn't
  built for 500 enemies on screen. Expect to cap live enemies (start at about 150),
  recycle off-screen enemies, and turn off loot drops from trash.

### Phase 3: Level-up loop
- XP orbs plus a magnet radius
- A paused choice screen with 3 cards (new skill / skill point / passive)
- Up to 5 skill slots and about 6 passives per run
- Target pacing: first level-up within about 20 s, then one every 20–40 s early on

### Phase 4: Run structure
- Lobby, then character select, then the run, then results
- Elites and chests, a boss every 5 minutes, the finisher at the end
- Gold and meta upgrades in the mod's own save file (`UserData/SurvivorsMod/meta.json`)

### Phase 5: Depth and content
- Evolutions (skill capstones plus a passive requirement)
- All 15 masteries as characters
- More stages (reuse existing zones), curse/difficulty modifiers, balance passes

---

## 6. Risks and how to handle them

| Risk | Mitigation |
|---|---|
| MelonLoader not yet supporting Unity 6.4 / metadata v39 | Check first (Phase 0). Wait for support rather than hand-patching the loader. |
| Game patches break hooks (it was patched today) | Name-based lookup, everything in `Adapter/`, a startup self-check, and noting which game version you tested against |
| Auto-cast not possible from code (input-bound) | Fallback: inject input through Rewired's custom controller API |
| Performance with large hordes | Live-enemy cap, pooling, simpler AI for distant enemies, fewer VFX |
| Run state leaking into the real save | Use a throwaway character, snapshot and restore, or block saving during runs |
| Offline mode may still route through networking code (`LE.Networking.Core`) | Confirm in Phase 1 that offline actions are resolved locally before relying on them |

---

## 7. Ground rules

- **Offline characters only.** Online play is server-authoritative and modifying
  the client there is a ToS problem. Have the mod disable itself (or refuse to
  load) if an online session is detected.
- **Don't use the `Last Epoch CT` (community tester) build** or anything learned
  from it. It's covered by your signed NDA. Build only against the public Steam build.
- **Distribute only the mod DLL and its data files.** Never redistribute game
  DLLs, generated interop assemblies or extracted assets.
- **Keep the design your own.** Use the genre's mechanics (auto-attack,
  hordes, level-up cards, timed runs) and LE's own skills and lore. Don't copy
  another game's item names, characters or art.

---

## 8. Open questions to decide before Phase 2

1. Run length: 15 or 30 minutes?
2. Should the player keep **any** manual control (e.g. a dash or one manual
   ultimate), or is it pure movement?
3. Skill pool: every LE skill, or a curated first set of about 10 that work well
   as auto-weapons?
4. Use a throwaway character per run, or snapshot and restore a real one?
5. Should meta progression ever touch real LE characters, or stay fully separate?
