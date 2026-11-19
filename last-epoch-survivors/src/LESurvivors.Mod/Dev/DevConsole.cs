using System;
using System.IO;
using System.Linq;
using System.Text;
using LESurvivors.Core;
using LESurvivors.Core.Content;
using LESurvivors.Mod.Game;
using UnityEngine;

namespace LESurvivors.Mod.Dev;

/// <summary>
/// File-driven developer console for automated in-game testing. Only active if UserData/LESurvivors/dev/enable exists.
/// Write commands (one per line) to dev/cmd.txt; results are appended to dev/out.log.
/// </summary>
internal sealed class DevConsole
{
    private readonly string _dir, _cmd, _out;
    private readonly SurvivorsMod _mod;
    private float _next;
    private readonly bool _enabled;

    public DevConsole(string dir, SurvivorsMod mod)
    {
        _dir = dir; _mod = mod;
        _cmd = Path.Combine(dir, "cmd.txt"); _out = Path.Combine(dir, "out.log");
        _enabled = File.Exists(Path.Combine(dir, "enable"));
        if (_enabled) Log.Info("Dev console enabled: " + dir);
    }

    private void Out(string s)
    {
        try { File.AppendAllText(_out, $"[{DateTime.Now:HH:mm:ss}] {s}\n"); } catch { }
        Log.Info("[dev] " + s);
    }

    public void Poll()
    {
        if (!_enabled || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        if (!File.Exists(_cmd)) return;
        string[] lines;
        try { lines = File.ReadAllLines(_cmd); File.Delete(_cmd); } catch { return; }
        foreach (var raw in lines)
        {
            var line = raw.Trim(); if (line.Length == 0) continue;
            try { Exec(line); } catch (Exception e) { Out($"ERR {line}: {e}"); }
        }
    }

    private void Exec(string line)
    {
        var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var h = RunHost.Instance;
        switch (a[0].ToLowerInvariant())
        {
            case "status":
                Out($"inGame={LE.InGame} offline={LE.IsOfflineCharacter} char={LE.CharacterName} class={LE.PlayerClass} pos={LE.PlayerPos} hp={LE.PlayerHealth}/{LE.PlayerMaxHealth} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} timeScale={Time.timeScale}");
                if (h != null) Out($"run state={h.Run.State} t={h.Run.Time:0.0} L{h.Run.Level} xp={h.Run.Xp:0}/{h.Run.XpNeeded:0} kills={h.Run.Kills} live={h.LiveEnemies} pickups={h.LivePickups} build={h.Run.Build.Summary()} choice={(h.Run.Choice == null ? "-" : string.Join(" | ", h.Run.Choice.Cards))}");
                break;
            case "shot":
                var p = Path.Combine(_dir, (a.Length > 1 ? a[1] : "shot") + ".png");
                ScreenCapture.CaptureScreenshot(p); Out("screenshot -> " + p); break;
            case "selfcheck":
                var probs = LE.SelfCheck(); Out(probs.Count == 0 ? "selfcheck OK" : "selfcheck: " + string.Join("; ", probs)); break;
            case "abilities":
                var cls = a.Length > 1 ? Enum.Parse<CharacterClass>(a[1], true) : LE.PlayerClass;
                var sb = new StringBuilder();
                foreach (var kv in LE.ClassAbilities(cls))
                    sb.Append($"\n  {kv.Key} tags={kv.Value.tags} cand={SkillCatalog.IsAutoWeaponCandidate(kv.Key, (Tag)(int)kv.Value.tags)} cps={kv.Value.chargesGainedPerSecond:0.00} mana={kv.Value.manaCost}");
                Out($"{cls} abilities:{sb}"); break;
            case "enemies":
                var pool = LE.EnemyPool(); var bosses = LE.BossPool();
                Out($"enemy pool {pool.Count}: " + string.Join(", ", pool.Take(60).Select(d => $"{d.actorName}(L{d.level})")));
                Out($"boss pool {bosses.Count}: " + string.Join(", ", bosses.Take(40).Select(d => $"{d.actorName}(L{d.level},{d.actorType})")));
                break;
            case "menu": _mod.Screens.MenuOpen = !_mod.Screens.MenuOpen; Out("menu=" + _mod.Screens.MenuOpen); break;
            case "start":
                int tier = a.Length > 1 ? int.Parse(a[1]) : 1;
                _mod.StartRun(new RunConfig(LE.PlayerClass, tier, Array.Empty<Omen>(), (ulong)DateTime.UtcNow.Ticks, _mod.Profile.Settings.EnemyCap));
                Out("start -> " + (RunHost.Instance != null)); break;
            case "choose": h?.Run.Choose(a.Length > 1 ? int.Parse(a[1]) : 0); Out("chose"); break;
            case "autochoose":
                while (h?.Run.Choice != null) { if (h.Run.Choice.Relic != null) h.Run.ResolveRelic(true); h.Run.Choose(0); }
                Out("auto-chose"); break;
            case "xp": h?.Run.GainXp(float.Parse(a[1])); Out("xp added"); break;
            case "cache": h?.Run.Collect(new Drop(DropKind.Cache, 1, (Rarity)int.Parse(a.Length > 1 ? a[1] : "2"))); Out("cache"); break;
            case "abandon": h?.Abandon(); Out("abandoned"); break;
            case "finish": h?.Finish(); Out("finished"); break;
            case "close": h?.Cleanup(); Out("closed"); break;
            case "overtime": h?.ContinueOvertime(); Out("overtime"); break;
            case "timescale": Time.timeScale = float.Parse(a[1]); Out("timescale " + Time.timeScale); break;
            case "heal": LE.HealPlayer(1f); Out("healed"); break;
            case "shards": _mod.Profile.Shards += long.Parse(a[1]); _mod.SaveProfile(); Out("shards " + _mod.Profile.Shards); break;
            case "perf":
                Out($"fps={1f / Time.smoothDeltaTime:0} dt={Time.deltaTime * 1000:0.0}ms live={h?.LiveEnemies} pickups={h?.LivePickups}"); break;
            default: Out("unknown command " + a[0]); break;
        }
    }
}
