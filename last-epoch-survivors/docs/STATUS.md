# Status and backlog

_Last updated: 2026-11-19_

## Done and verified
| Item | Evidence |
|---|---|
| MelonLoader 0.7.3 runs on LE (Unity 6000.4.8f1, metadata v39) | Interop generation succeeded on PowerStorm |
| Fix for the `No Support Module Loaded!` blocker | `tools/InteropFix` renamed 48 duplicate `<>O` stubs. After that the log shows `Support Module Loaded` |
| Class map (recon) of the game code | Key APIs recorded below |
| Core game logic | `dotnet test`: 38/38 pass, including per-class Tier I win-rate sims, tier/meta difficulty checks and save/backup recovery |
| Mod compiles against the real interop assemblies and loads | MelonLoader log: `LE Survivors v0.1.0`, 4 Harmony patches applied, dev console enabled |

## Blocked: needs the user
- **New Terms of Service / Privacy Policy prompt (effective 2026-10-01)** at the game's Login screen. Accepting it is
  the account owner's decision, so it was not clicked automatically. In-game testing resumes once it is accepted
  and an offline character is loaded.

## Next (in order)
1. In-game smoke test: auto-cast via `AbilityObjectConstructor.constructAbilityObject`, enemy spawning via `ActorData.spawn`,
   the death hook, the pickup visuals and the stat buffs (`StatBuffs.addBuff` value conventions: fraction or percent).
2. Performance: find the live-enemy count where frame time stays under 16.7 ms, then set the default cap.
3. Curate the skill table from the real class ability list (dev command `abilities`): intervals and targeting.
4. Enemy pool curation (dev command `enemies`): level bands and visual variety. Choose Warden and Harbinger candidates.
5. Balance pass in game: enemy health and damage multipliers against the auto-cast damage scale.
6. Polish: the elite and boss visuals, sound cues on level-up and cache, and damage numbers.

## Key LE APIs (from recon)
| Need | API |
|---|---|
| Player | `PlayerFinder.getPlayerActor()`, `getPlayerData()` (`IsOffline`, `_Hardcore_k__BackingField`, `_CharacterClass_k__BackingField`) |
| Cast without an animation lock | `Actor.abilityObjectConstructor.constructAbilityObject(ability, from, target, UseType.Indirect, …, mutator, …, abilityInfo, …)` |
| Per-cast stats | `AbilityObjectConstructor.AddStatForNextAbilityUse(new Stats.Stat(SP, AT, added, increased, more, 0, 0))` |
| Global stats | `Actor.statBuffs.addBuff(duration, SP, added, increased, List<float> more, AT, name)` / `removeBuffsWithName` |
| Class skills | `CharacterClassList.getClass(id).knownAbilities` + `masteries[].abilities` |
| Enemies | `ActorDataList.instance().data` (filter: `spawnsInArena`, `actorType`), `ActorData.spawn(...)`, `Actor.setRarity` |
| Death | `Dying.die` (patched), `DeathItemDrop.OnDeath` (patched to block loot and XP) |
| Character XP | `ExperienceTracker.GainExp` / `GainExpFromEnemyOrMote` (patched off during runs) |
| Health | `BaseHealth.AddSourceOfCannotDie` / `RemoveSourceOfCannotDie`, `heal`, `SetHealthToFull` |
| Arena (later) | `WaveSpawner`, `ArenaRunManager`, `SceneList.IsArena` |
