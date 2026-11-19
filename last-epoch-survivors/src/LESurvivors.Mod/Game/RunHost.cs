using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LESurvivors.Core;
using LESurvivors.Core.Content;
using UnityEngine;
using CharacterClass = LESurvivors.Core.Content.CharacterClass;

namespace LESurvivors.Mod.Game;

/// <summary>Runs a Core.Run inside Last Epoch: spawning, auto-casting, pickups, stats, death and cleanup.</summary>
internal sealed class RunHost
{
    public static RunHost Instance { get; private set; }
    public static bool Active => Instance != null && Instance.Run.IsActive;

    public Run Run { get; }
    public RunConfig Config { get; }
    public RunResults Results { get; private set; }
    public bool Finished => Results != null;
    public string LastWarning { get; private set; } = "";

    private sealed class Enemy { public Actor Actor; public EnemyKind Kind; public float RepathAt; public int Id; }
    private readonly Dictionary<int, Enemy> _enemies = new();          // by GameObject instance id
    private readonly Dictionary<string, Ability> _abilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<OwnedSkill, float> _timers = new();
    private readonly Dictionary<OwnedSkill, int> _castCounts = new();
    private readonly Queue<SpawnRequest> _spawnQueue = new();
    private readonly List<(float at, Action act)> _delayed = new();
    private readonly List<ActorData>[] _poolByTier = new List<ActorData>[5];
    private List<ActorData> _bosses = new();
    private readonly Pickups _pickups = new();
    private readonly Profile _profile;
    private readonly Action<Profile> _saveProfile;
    private readonly System.Random _r = new();
    private float _lodestoneUntil, _freezeUntil, _procBudget, _deathGrace;
    private bool _cannotDieAdded;
    private int _kills;

    public int LiveEnemies => _enemies.Count;
    public int LivePickups => _pickups.Count;

    private RunHost(RunConfig cfg, Profile profile, Action<Profile> save)
    {
        Config = cfg; _profile = profile; _saveProfile = save;
        var pool = LE.BuildSkillPool(cfg.Class);
        foreach (var (def, ab) in pool) _abilities[def.Id] = ab;
        if (pool.Count == 0) throw new InvalidOperationException("No usable skills found for " + cfg.Class);
        Run = new Run(cfg, pool.Select(p => p.def).ToList(), profile);
        BuildEnemyPools();
        Run.SpawnRequested += r => _spawnQueue.Enqueue(r);
        Run.StatsChanged += ApplyStats;
        Run.HealRequested += f => LE.HealPlayer(f);
        Run.Ended += OnEnded;
        Run.ChoiceOpened += _ => { if (_profile.Settings.PauseOnLevelUp) Time.timeScale = 0f; };
    }

    public static RunHost Start(RunConfig cfg, Profile profile, Action<Profile> save)
    {
        if (Instance != null && !Instance.Finished) throw new InvalidOperationException("A run is already active");
        Instance?.Cleanup();
        var h = new RunHost(cfg, profile, save);
        Instance = h;
        profile.RunsStarted++;
        save(profile);
        h.ApplyStats();
        var hp = LE.Player?.health;
        if (hp != null) { hp.AddSourceOfCannotDie(); h._cannotDieAdded = true; hp.SetHealthToFull(); }
        Log.Info($"Run started: {cfg.Class} tier {cfg.Tier}, omens [{string.Join(",", cfg.Omens)}], skills in pool {h._abilities.Count}, enemy types {h._poolByTier.Sum(p => p.Count)}");
        return h;
    }

    private void BuildEnemyPools()
    {
        var all = LE.EnemyPool().OrderBy(d => d.level).ToList();
        if (all.Count == 0) throw new InvalidOperationException("No arena-capable enemy types found");
        // five tiers by level band; each tier also keeps the previous tier's types for variety
        for (int t = 0; t < 5; t++)
        {
            int lo = all.Count * t / 6, hi = Math.Max(lo + 1, all.Count * (t + 2) / 6);
            _poolByTier[t] = all.GetRange(lo, Math.Min(hi, all.Count) - lo);
        }
        _bosses = LE.BossPool();
        if (_bosses.Count == 0) _bosses = all.Skip(all.Count * 3 / 4).ToList();
    }

    public void ApplyStats()
    {
        try { StatBridge.ApplyGlobal(Run.Build.Global); }
        catch (Exception e) { LastWarning = "stats: " + e.Message; Log.Warn(LastWarning); }
    }

    // ------------------------------------------------------------------ frame

    public void Update(float dt)
    {
        if (Finished) return;
        var player = LE.Player;
        if (player == null) { Run.Abandon(); return; }
        if (Run.State == RunState.Choosing) return;
        if (Run.State == RunState.Running && Time.timeScale == 0f) Time.timeScale = 1f;
        if (Run.State != RunState.Running) return;

        Run.Tick(dt, _enemies.Count);
        ProcessSpawns(player.transform.position);
        UpdateEnemies(player.transform.position, dt);
        AutoCast(player, dt);
        RunDelayed();
        UpdatePickups(player.transform.position, dt);
        CheckPlayerHealth(dt);
        _procBudget = Mathf.Min(4f, _procBudget + dt * 4f);
    }

    private void RunDelayed()
    {
        float now = Run.Time;
        for (int i = _delayed.Count - 1; i >= 0; i--)
        {
            if (_delayed[i].at > now) continue;
            var a = _delayed[i].act; _delayed.RemoveAt(i);
            try { a(); } catch (Exception e) { Log.Warn("delayed: " + e.Message); }
        }
    }

    // ------------------------------------------------------------------ spawning

    private const int MaxSpawnsPerFrame = 6;

    private void ProcessSpawns(Vector3 center)
    {
        int budget = MaxSpawnsPerFrame;
        while (budget > 0 && _spawnQueue.Count > 0)
        {
            var r = _spawnQueue.Peek();
            float baseAngle = float.IsNaN(r.DirectionDeg) ? UnityEngine.Random.Range(0f, 360f) : r.DirectionDeg;
            int n = Math.Min(r.Count, budget);
            var data = PickData(r);
            for (int i = 0; i < n; i++)
            {
                float ang = (baseAngle + UnityEngine.Random.Range(-14f, 14f)) * Mathf.Deg2Rad;
                float dist = UnityEngine.Random.Range(17f, 23f);
                var want = center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist;
                if (!LE.TryNavPoint(want, 6f, out var pos) && !LE.TryNavPoint(center + (want - center) * 0.5f, 6f, out pos)) continue;
                var a = LE.SpawnEnemy(data, pos);
                if (a == null) continue;
                var sc = Run.Director.Scaling(Run.Time, r.Kind);
                LE.Scale(a, sc.Health, sc.Speed, r.Kind);
                int id = a.gameObject.GetInstanceID();
                _enemies[id] = new Enemy { Actor = a, Kind = r.Kind, Id = id };
            }
            budget -= n;
            if (n >= r.Count) _spawnQueue.Dequeue();
            else { _spawnQueue.Dequeue(); _spawnQueue.Enqueue(r with { Count = r.Count - n }); }
        }
    }

    private ActorData PickData(SpawnRequest r)
    {
        if (r.Kind >= EnemyKind.Warden && _bosses.Count > 0) return _bosses[_r.Next(_bosses.Count)];
        var pool = _poolByTier[Math.Clamp(r.PoolTier, 0, 4)];
        return pool[_r.Next(pool.Count)];
    }

    public static bool IsRunEnemy(GameObject go) => Instance != null && go != null && Instance._enemies.ContainsKey(go.GetInstanceID());

    private void UpdateEnemies(Vector3 player, float dt)
    {
        bool frozen = Run.Time < _freezeUntil;
        var toRemove = new List<int>();
        foreach (var e in _enemies.Values)
        {
            if (LE.IsDead(e.Actor)) { toRemove.Add(e.Id); continue; }
            var pos = e.Actor.transform.position;
            float d = Vector3.Distance(pos, player);
            var agent = e.Actor.navMeshAgent;
            if (agent == null || !agent.isOnNavMesh) continue;
            if (frozen) { agent.isStopped = true; continue; }
            if (agent.isStopped) agent.isStopped = false;
            if (d > 45f && e.Kind == EnemyKind.Normal)
            {   // recycle stragglers to the spawn ring in front of the player
                float ang = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                if (LE.TryNavPoint(player + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 20f, 6f, out var p)) agent.Warp(p);
                continue;
            }
            if (d > 9f && Run.Time >= e.RepathAt)
            {   // horde behaviour: everything converges on the player; LE's own AI takes over in melee range
                e.RepathAt = Run.Time + 0.5f + (float)_r.NextDouble() * 0.3f;
                agent.SetDestination(player);
            }
        }
        foreach (var id in toRemove) _enemies.Remove(id);
    }

    public void OnActorDied(GameObject go)
    {
        if (go == null) return;
        int id = go.GetInstanceID();
        if (!_enemies.TryGetValue(id, out var e)) return;
        _enemies.Remove(id);
        var pos = go.transform.position;
        _kills++;
        foreach (var d in Run.OnEnemyKilled(e.Kind))
        {
            if (d.Kind == DropKind.Cache || d.Kind == DropKind.HealthOrb || d.Kind == DropKind.Lodestone || d.Kind == DropKind.TimeFracture || d.Kind == DropKind.XpMote)
                _pickups.Spawn(d, pos);
        }
        Procs(e, pos);
        if (Run.Build.HasPower(RelicPower.GravebloomSeed)) LE.HealPlayer(0.005f);
    }

    // ------------------------------------------------------------------ procs (Resonance III, reactions)

    private void Procs(Enemy dead, Vector3 pos)
    {
        var b = Run.Build;
        if (b.ActiveProcs.Count == 0 || _procBudget < 1f) return;
        void Echo(Tag tag, double chance)
        {
            if (_r.NextDouble() > chance || _procBudget < 1f) return;
            var s = b.Skills.FirstOrDefault(x => (x.Def.Tags & tag) != 0 && x.Def.Targeting is Targeting.Self or Targeting.Densest)
                    ?? b.Skills.FirstOrDefault(x => (x.Def.Tags & tag) != 0);
            if (s == null || !_abilities.TryGetValue(s.Def.Id, out var ab)) return;
            _procBudget -= 1f;
            LE.Cast(ab, pos, pos + Vector3.forward, StatBridge.CastStats(b, s));
        }
        if (b.ActiveProcs.Contains(Proc.FireNovaOnIgnitedKill)) Echo(Tag.Fire, 0.18);
        if (b.ActiveProcs.Contains(Proc.FrostBurstOnFrozenKill)) Echo(Tag.Cold, 0.18);
        if (b.ActiveProcs.Contains(Proc.BloodBurstOnBleedKill)) Echo(Tag.Physical, 0.15);
        if (b.ActiveProcs.Contains(Proc.PoisonCloudOnKill)) Echo(Tag.Poison, 0.18);
        if (b.ActiveProcs.Contains(Proc.SoulOnKill)) Echo(Tag.Necrotic, 0.15);
        if (b.ActiveProcs.Contains(Proc.Shatter) && _procBudget >= 1f)
        {
            _procBudget -= 1f;
            float burst = (dead.Actor?.health != null ? dead.Actor.health.maxHealth : 50) * 0.3f;
            foreach (var e in _enemies.Values)
                if (!LE.IsDead(e.Actor) && Vector3.Distance(e.Actor.transform.position, pos) < 4f) e.Actor.health.HealthDamage(burst);
        }
    }

    // ------------------------------------------------------------------ auto-cast

    private void AutoCast(Actor player, float dt)
    {
        var b = Run.Build;
        var from = player.transform.position;
        bool hourglass = Run.Time < Run.HourglassUntil;
        foreach (var s in b.Skills)
        {
            _timers.TryGetValue(s, out var t);
            t -= dt * (hourglass ? 1.25f : 1f);
            if (t > 0) { _timers[s] = t; continue; }
            if (!_abilities.TryGetValue(s.Def.Id, out var ab)) { _timers[s] = 1f; continue; }
            int casts = b.CastsPerTrigger(s);
            _castCounts.TryGetValue(s, out var cc);
            if (Run.Build.HasPower(RelicPower.Chronoshard) && (cc + 1) % 10 == 0) casts++;
            _castCounts[s] = cc + 1;
            var targets = PickTargets(s, from, casts);
            if (targets.Count == 0 && s.Def.Targeting != Targeting.Self) { _timers[s] = 0.15f; continue; } // nothing in range: retry soon
            var stats = StatBridge.CastStats(b, s);
            for (int i = 0; i < casts; i++)
            {
                var target = targets.Count > 0 ? targets[i % targets.Count] : from + player.transform.forward * 3f;
                int idx = i;
                void Do()
                {
                    var p = LE.Player; if (p == null) return;
                    if (LE.Cast(ab, p.transform.position, target, stats)) { s.Casts++; }
                }
                if (idx == 0) Do(); else _delayed.Add((Run.Time + 0.08f * idx, Do));
            }
            _timers[s] = b.IntervalOf(s);
        }
    }

    private List<Vector3> PickTargets(OwnedSkill s, Vector3 from, int count)
    {
        var res = new List<Vector3>();
        var mode = _profile.Settings.Targeting switch
        {
            TargetingOverride.Nearest when s.Def.Targeting != Targeting.Self => Targeting.Nearest,
            TargetingOverride.Densest when s.Def.Targeting != Targeting.Self => Targeting.Densest,
            _ => s.Def.Targeting,
        };
        float range = Math.Max(s.Def.Range, 3f);
        if (mode == Targeting.Self) { res.Add(from); return res; }
        if (mode == Targeting.MoveDirection)
        {
            var fwd = LE.Player.transform.forward; res.Add(from + fwd * Math.Max(4f, range * 0.6f)); return res;
        }
        var near = new List<(float d, Vector3 p)>();
        foreach (var e in _enemies.Values)
        {
            if (LE.IsDead(e.Actor)) continue;
            var p = e.Actor.transform.position; float d = Vector3.Distance(p, from);
            if (d <= range) near.Add((d, p));
        }
        if (near.Count == 0) return res;
        near.Sort((a, b) => a.d.CompareTo(b.d));
        if (mode == Targeting.Densest)
        {
            var sample = near.Take(40).ToList();
            var best = sample.OrderByDescending(c => sample.Count(o => (o.p - c.p).sqrMagnitude < 16f)).First();
            res.Add(best.p);
            foreach (var n in near) { if (res.Count >= count) break; if ((n.p - best.p).sqrMagnitude > 9f) res.Add(n.p); }
            return res;
        }
        if (mode == Targeting.RandomInRange) { for (int i = 0; i < count; i++) res.Add(near[_r.Next(near.Count)].p); return res; }
        foreach (var n in near) { res.Add(n.p); if (res.Count >= count) break; }
        return res;
    }

    // ------------------------------------------------------------------ pickups & health

    private void UpdatePickups(Vector3 player, float dt)
    {
        float radius = 3.5f * (1f + Run.Build.Global.Get(Stat.PickupRadius) / 100f);
        foreach (var d in _pickups.Update(player, radius, dt, Run.Time < _lodestoneUntil))
        {
            switch (d.Kind)
            {
                case DropKind.Lodestone: _lodestoneUntil = Run.Time + 1.5f; break;
                case DropKind.TimeFracture: _freezeUntil = Run.Time + d.Value; break;
                default: Run.Collect(d); break;
            }
        }
    }

    private void CheckPlayerHealth(float dt)
    {
        if (_deathGrace > 0) { _deathGrace -= dt; return; }
        if (LE.PlayerHealth > Math.Max(1f, LE.PlayerMaxHealth * 0.005f)) return;
        if (Run.OnPlayerDied())
        {
            _deathGrace = 2f;
            LE.HealPlayer(1f);
            _freezeUntil = Run.Time + 2f; // a short breather after a revive
        }
    }

    // ------------------------------------------------------------------ end

    private void OnEnded(RunState s)
    {
        if (s == RunState.Victory) { Time.timeScale = 0f; return; } // victory screen offers Overtime
        Finish();
    }

    /// <summary>Called by the UI after victory: continue into overtime or finish.</summary>
    public void ContinueOvertime() { Run.ContinueIntoOvertime(); Time.timeScale = 1f; }

    public void Finish()
    {
        if (Results != null) return;
        Results = Run.Results();
        Run.Commit(_profile, Config, Results);
        _saveProfile(_profile);
        Log.Info($"Run ended: {Results.Outcome} {Results.Time / 60:0.0}m L{Results.Level} kills {Results.Kills} shards {Results.Shards}");
        Time.timeScale = 0f;
        CleanupWorld();
    }

    public void Abandon() { Run.Abandon(); }

    private void CleanupWorld()
    {
        foreach (var e in _enemies.Values)
        {
            try { if (e.Actor != null && !e.Actor.WasCollected) UnityEngine.Object.Destroy(e.Actor.gameObject); } catch { }
        }
        _enemies.Clear(); _spawnQueue.Clear(); _delayed.Clear();
        _pickups.Destroy();
        LE.ClearRunBuffs();
        var hp = LE.Player?.health;
        if (hp != null)
        {
            if (_cannotDieAdded) { hp.RemoveSourceOfCannotDie(); _cannotDieAdded = false; }
            hp.SetHealthToFull();
        }
    }

    /// <summary>Close the results screen and release everything.</summary>
    public void Cleanup()
    {
        if (Results == null && Run.IsActive) { Run.Abandon(); }
        if (Results == null) Finish();
        CleanupWorld();
        Time.timeScale = 1f;
        if (Instance == this) Instance = null;
    }
}
