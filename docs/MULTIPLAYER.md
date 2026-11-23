# Planned: self-hosted multiplayer server

Status: planned, not started. Requested by the project owner on 2026-11-20.
Order: after GTA 1 single-player is finished (same rule as GTA 2). Do not drop this.

Owner's wording: "Maybe not an MMO, but have some kind of self hosted server for people to have a
large multiplayer experience?" and "Keep going on single-player first, but make sure this is
something not to be forgotten about."

## Goal
A dedicated server program anyone can run themselves, with a city full of players. No accounts or
always-on persistent world required.

## Shape
- Server: the headless Linux build extended to accept network players (Windows build later). It
  owns the simulation: traffic, pedestrians, police, damage, scores.
- Clients: the Android and Windows builds get a "Join server" entry taking an address. Each player
  needs their own original GTA data.
- Server-authoritative state replication with client smoothing. Not lockstep: physics is not
  bit-identical between ARM and x86.
- Target 16-32 players per city on home hardware, up to 64 with interest management. Prove 8 first.

## Modes
- Free roam with shared police / wanted levels.
- Deathmatch and score races (the original game's multiplayer modes).
- Shared missions: new design work, later.

## Steps
1. Server runs a city with no local player; one remote client joins and drives.
2. Several clients, lag smoothing, join/leave mid-game.
3. Shared traffic, pedestrians, police, combat.
4. Game modes, server settings file, server list or direct-address join.

## Notes
- The engine already supports four players in one process (split-screen), and has no networking.
- Single-player rule recovery (explosions, damage, doors, pedestrians) is reused by the server.
- A proof of concept of step 1 can be slotted in early if the owner asks.
