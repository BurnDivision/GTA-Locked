# LE Survivors: Game Design (v1)

Design references, in priority order:
1. **Soulstone Survivors**: combat intensity, combinations of skill tags, rarity-tiered upgrades,
   power that keeps escalating, curses, a deep meta tree.
2. **Halls of Torment**: ARPG feel, equipment slots, character development.
3. **Death Must Die**: build-defining randomized powers and itemization.
4. **Vampire Survivors**: the base loop, weapon evolution, accessibility.

The foundation is **Last Epoch's own systems**: its classes, real skill objects (with their VFX,
damage types and ailments), its stat system (`SP` stats and `AT` tags through `StatBuffs`) and its enemies.
Every number below lives in `Balance.cs` / `Data/*.json` and gets tuned by simulation and play.
No asset, name or text from another game is copied.

---

## 1. The run

| | |
|---|---|
| Length | **20:00** main phase. A final boss spawns at 20:00. Killing it wins the run, and the player can then stay in **Overtime** (endless, difficulty +8%/min) for a score multiplier |
| Where | The zone the player is in, which should be open ground (the Arena of Memory is ideal). Spawns are sampled on the zone's NavMesh in a ring outside the camera. No scene loading is needed |
| Character | The player's **offline** character. The class decides the skill pool. **Saving is suppressed for the whole run**, and every run effect is a temporary buff, so the character is unchanged afterwards. A fresh level-1 character is recommended and is what the balance targets |
| Lose | The player dies (after any revives are spent) |
| Win | The 20:00 boss dies |

### Phases (default curve, Epoch I)
| Time | Event |
|---|---|
| 0:00 | Trickle of normal enemies. First level-up within about 15 s |
| every 60 s | An elite (Magic or Rare LE rarity). It drops a **Cache** (chest) |
| 5:00 / 10:00 / 15:00 | **Warden** mini-boss: a big Rare unit with a health multiplier. It drops a guaranteed Cache with Exalted or better |
| 7:30 / 12:30 / 17:30 | **Surge**: 20 seconds of triple spawn rate from one direction |
| 20:00 | **Harbinger of the Epoch**: the final boss. Trash spawns stop while it lives |

Target live enemy count: `C(t) = 30 + 270 · smoothstep(t / 20 min)`, capped by the performance
setting (default 220; a range of 120 to 400). Spawns per second come from a budget that refills
toward `C(t)`. Enemies more than 45 m from the player are recycled to the spawn ring.

---

## 2. Active skills (auto-cast)

- The skill pool is the class's **real LE player abilities**, filtered to ones that work as
  auto-weapons (no movement, transform, channel or minion-command skills in v1).
- **6 active slots.** The start is 1 skill (class default) with 4 slots unlocked; the meta tree unlocks the 5th and 6th.
- Casting goes through `AbilityObjectConstructor.constructAbilityObject`, the engine path LE uses for
  triggered casts. It needs no animation lock or mana, the player keeps moving, and LE's own
  VFX, damage, ailments and the character's skill-tree mutators still apply.
- Each skill has a **mod-side cast interval**: `interval = base / (1 + castFrequency)`.
  The base comes from a per-skill table (`Data/skills.json`), with a fallback of `max(0.6 s, LE cooldown)`.
- **Targeting** per skill: `Nearest`, `Densest` (largest cluster within range), `RandomInRange`,
  `Self` (novas and auras), `MoveDirection`. The player can override it globally in settings.
- **Multicast**: extra casts at the next-nearest targets, staggered 0.08 s apart.
- Per-skill stats are passed into the cast as temporary stats (`AddStatForNextAbilityUse`):
  increased damage, area, crit chance and ailment chance. Global stats use `StatBuffs`.

### Skill ranks
Ranks 1 to 10. Each level-up card for an owned skill raises its rank and applies one **upgrade
line** at a rolled **rarity**.

| Rarity (LE colours) | Weight (base) | Magnitude |
|---|---|---|
| Normal (white) | 60 | ×1.0 |
| Magic (blue) | 26 | ×1.5 |
| Rare (yellow) | 10 | ×2.25 |
| Exalted (purple) | 3.5 | ×3.25 |
| Legendary (orange) | 0.5 | ×5.0 |

Luck (from the meta tree, relics and curses) moves weight from Normal to the higher tiers.

Skill upgrade lines (base value at Normal): +12% damage, +10% area, +10% cast frequency,
+8% crit chance, +15% ailment chance, +1 multicast (Rare and above only; it counts as two lines).

---

## 3. Passives ("Glyphs") and tag synergy

**Glyphs** are passive cards that raise global stats, usually scoped to an **LE ability tag**
(`AT.Fire`, `AT.Cold`, `AT.Lightning`, `AT.Physical`, `AT.Void`, `AT.Necrotic`, `AT.Poison`,
`AT.Spell`, `AT.Melee`, `AT.Throwing`, `AT.Bow`, `AT.DoT`, `AT.Minion`). Examples:
- Glyph of Embers: +X% increased **Fire** damage, +Y% ignite chance
- Glyph of the Storm: +X% increased **Lightning** damage, +shock chance
- Glyph of Haste: +movement speed
- Glyph of Vitality: +added health, +health regen
- Glyph of Reach: +pickup radius
- Glyph of Fortune: +luck, +XP gain
- Glyph of Precision: +crit chance, +crit multiplier
- Glyph of Celerity: +global cast frequency
- Glyph of Bulwark: +armour, +all resistances

There are 6 Glyph slots, and each Glyph has ranks 1 to 5. Rarity scales each pick the same way it does for skills.

### Resonance (the synergy engine)
Every owned skill **and** Glyph adds its tags to a running count. Reaching a threshold activates
a **Resonance tier** for that tag:

| Tier | Needs | Effect type |
|---|---|---|
| I | 2 | Stat bonus (for example Fire I gives +20% Fire damage and +10% ignite chance) |
| II | 4 | Bigger bonus plus an ailment interaction (Fire II: ignited enemies take 15% more damage from all sources) |
| III | 6 | **Proc**: a real LE ability is cast on certain events (Fire III: killing an ignited enemy casts a fire nova at its corpse, at most 4 per second) |

**Cross-element reactions** (Soulstone-style combos built on LE ailments) activate when two Tier-II resonances are both active:
- **Shatter** (Cold + Physical): frozen enemies that are killed burst for 30% of their max health as damage to nearby enemies
- **Overload** (Lightning + Fire): shock duration +100%, and shocked enemies spread ignite
- **Blight** (Poison + Necrotic): poison stacks also lower necrotic resistance
- **Rift** (Void + any element): Void hits echo once at 30% damage

---

## 4. Ascension (evolutions)
A skill at **rank 10** whose primary damage tag has **Resonance II** becomes *Ascension-ready*.
The next Cache or Warden drop offers **Ascend [Skill]**:
- +1 multicast, ×1.75 more damage with that skill, +25% area
- plus a signature effect drawn from the skill's tag family (pierce or chain for projectiles,
  a second pulse for areas, and so on)

This plays the role of weapon evolution while keeping LE's skill identity.

---

## 5. Relics (equipment, in the spirit of Halls of Torment and Death Must Die)
- **4 relic slots** for the run: Amulet, Ring, Talisman and Idol (the 4th slot unlocks through the meta tree).
- Caches roll relics with LE-style affixes. Normal has 1 affix, Magic 2, Rare 3 and Exalted 4,
  where one affix is tier 6-7. A **Legendary** relic carries a fixed **signature power**
  (for example *Chronoshard*: every 10th cast of each skill happens twice; *Ember Heart*: ignite
  stacks also add 1% more Fire damage per stack; *Warden's Coil*: block chance becomes
  damage reduction).
- A new relic either replaces the one in its slot or is **salvaged** for Shards.

---

## 6. Level-up flow
- **XP motes** drop from kills and come in 3 sizes (1, 5 and 25 XP). They are magnetised within the pickup radius.
- XP needed for level n: `5 + 3n + 0.035·n²` (about 75 levels in a clean 20 minutes).
- A level-up **pauses the game** and offers 3 cards (4 with the meta node). Each card is a new skill,
  a skill rank, a new Glyph, a Glyph rank, or a filler (heal 30% or Shards) once everything is full.
- **Reroll**, **Banish** (the card's subject never appears again this run) and **Skip** (+XP) are charged resources from the meta tree.
- Card generation is weighted. Owned subjects get ×1.6 weight, ascension-ready skills ×1.3, and new skills are suppressed once the slots are full.

---

## 7. Pickups
- **Health orb** (from breakables or 1% of kills): heals 20% of max health
- **Lodestone** (rare): pulls in every mote on the map
- **Time Fracture** (rare): freezes enemies for 4 s
- **Shard cluster**: meta currency

---

## 8. Curses and difficulty (LE's "Corruption")
**Epoch tiers I to X.** Each tier adds about 18% enemy health, 10% enemy damage, +1 to the enemy level range
and +15% to rewards. Beating a tier unlocks the next.

**Omens** are optional, stackable curses chosen before the run. Each adds Corruption, and rewards gain
`×(1 + Corruption/100)`:
| Omen | Effect | Corruption |
|---|---|---|
| Swarm | +35% live enemy cap and spawn rate | 25 |
| Brutality | +30% enemy damage | 25 |
| Haste | +15% enemy movement speed | 20 |
| Tenacity | +40% elite and boss health | 20 |
| Famine | Health orbs are 50% rarer and healing is 30% weaker | 15 |
| Frailty | −20% max health | 20 |
| Ambush | Surges come twice as often | 15 |

---

## 9. Meta progression ("The Obelisk")
The currency is **Time Shards**, earned from kills, Caches, bosses and win bonuses, then scaled by tier and Corruption.
The tree (`Data/meta.json`) has about 30 nodes with ranks. Examples:
- **Vitality** (5): +6% max health per rank
- **Potency** (5): +5% damage
- **Insight** (5): +5% XP
- **Reach** (3): +15% pickup radius
- **Fortune** (5): +5 luck
- **Second Wind** (2): +1 revive per run
- **Reroll / Banish / Skip** (3 each)
- **Arsenal** (2): unlock the 5th and 6th active slots
- **Reliquary**: unlock the 4th relic slot
- **Foresight**: offer 4 cards instead of 3
- **Mastery unlocks** (one node per mastery): each starts the run with that mastery's signature skill and a +10% tag bonus
- **Epoch Gate**: unlocks Omens

Meta progress is saved in the mod's own file, `UserData/LESurvivors/profile.json`. It never
touches LE save data. It is written atomically (temp file + rename) and a backup is kept.

---

## 10. End of run
The results screen shows the outcome, time survived, kills, damage per skill (counted from casts and hits),
level reached, Shards earned (with a breakdown), the build summary (skills, Glyphs, relics, Resonances) and
newly unlocked content.

---

## 11. Settings and accessibility
These are a mod config page plus MelonPreferences: the enemy cap (performance), a global targeting mode,
auto-pause on level-up (on/off), card text size, colour-blind rarity icons (shapes as well as colour),
screen-shake toggle, damage number density and the hotkeys (open the menu, pause).
