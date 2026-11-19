using System;
using System.Collections.Generic;
using System.Linq;
using LESurvivors.Core;
using LESurvivors.Core.Content;
using LESurvivors.Mod.Game;
using UnityEngine;
using static LESurvivors.Mod.UI.Gui;

namespace LESurvivors.Mod.UI;

/// <summary>All Survivors screens. Draw() is called from OnGUI.</summary>
internal sealed class Screens
{
    public bool MenuOpen;
    public bool PauseOpen;
    private int _tab;
    private int _tier = 1;
    private readonly HashSet<Omen> _omens = new();
    private bool _banishMode;
    private string _toast = ""; private float _toastUntil;
    private readonly Func<Profile> _profile;
    private readonly Action _save;
    private readonly Action<RunConfig> _start;

    public Screens(Func<Profile> profile, Action save, Action<RunConfig> start)
    { _profile = profile; _save = save; _start = start; }

    public void Toast(string s) { _toast = s; _toastUntil = UnityEngine.Time.unscaledTime + 4f; }

    private static string RarityTag(Rarity r, bool shapes) => Col(Rarities.Colour[(int)r], (shapes ? Rarities.Shape[(int)r] + " " : "") + r);

    public void Draw()
    {
        Gui.Scale = _profile().Settings.UiScale * Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
        Begin();
        var host = RunHost.Instance;
        if (host != null)
        {
            if (host.Finished) DrawResults(host);
            else
            {
                DrawHud(host);
                if (host.Run.State == RunState.Victory && !host.Run.OvertimeChosen) DrawVictory(host);
                else if (host.Run.Choice != null) DrawChoice(host);
                else if (PauseOpen) DrawPause(host);
            }
        }
        else if (MenuOpen) DrawMenu();
        if (UnityEngine.Time.unscaledTime < _toastUntil)
        {
            var r = R(Screen.width / 2f - S(300), S(90), S(600), S(40)); Panel(r); Center(r, _toast);
        }
    }

    // ------------------------------------------------------------------ HUD

    private void DrawHud(RunHost h)
    {
        var run = h.Run; var b = run.Build; bool shapes = _profile().Settings.ShowRarityShapes;
        float w = Screen.width;
        // timer
        var tr = R(w / 2 - S(110), S(8), S(220), S(56)); Panel(tr, 0.7f);
        bool over = run.Time > Director.RunLength;
        Title(R(tr.x, tr.y + S(2), tr.width, S(32)), over ? Col("#FF7A3D", "+" + Gui.Time(run.Time - Director.RunLength)) : Gui.Time(Director.RunLength - run.Time));
        Small(R(tr.x + S(8), tr.y + S(34), tr.width, S(20)), $"Epoch {ToRoman(run.Config.Tier)}  ·  Corruption {run.Corruption}  ·  {run.Kills} kills");
        if (run.Director.InSurge(run.Time)) Center(R(w / 2 - S(200), S(66), S(400), S(26)), Col("#FF5050", "⚠ SURGE ⚠"));
        if (run.Director.HarbingerAlive) Center(R(w / 2 - S(250), S(66), S(500), S(26)), Col("#FF8A2B", "The Harbinger of the Epoch has arrived"));
        // xp bar
        var xr = R(S(10), Screen.height - S(26), w - S(20), S(16));
        Bar(xr, run.Xp / run.XpNeeded, new Color(0.35f, 0.65f, 1f), new Color(0, 0, 0, 0.6f));
        Center(xr, $"Level {run.Level}");
        // build panel
        float y = S(80); var pr = R(S(10), y, S(300), S(26 + 22 * (b.Skills.Count + b.Glyphs.Count + b.ResonanceTier.Count + b.Relics.Count) + 40));
        Panel(pr, 0.6f);
        float ly = y + S(6);
        foreach (var s in b.Skills)
        {
            Label(R(S(18), ly, S(290), S(22)), $"{(s.Ascended ? Col("#FF8A2B", "★ ") : "")}{s.Def.Name} {Col("#B0A080", "r" + s.Rank)}  {Col("#808080", $"×{b.CastsPerTrigger(s)} / {b.IntervalOf(s):0.00}s")}");
            ly += S(22);
        }
        foreach (var g in b.Glyphs) { Label(R(S(18), ly, S(290), S(22)), Col("#9FD3FF", g.Def.Name) + $" {g.Rank}"); ly += S(22); }
        foreach (var (tag, tier) in b.ResonanceTier) { Label(R(S(18), ly, S(290), S(22)), Col("#E7C46B", $"◆ {tag} Resonance {ToRoman(tier)}")); ly += S(22); }
        foreach (var r in b.Relics.Values) { Label(R(S(18), ly, S(290), S(22)), RarityTag(r.Rarity, shapes) + " " + r.Name); ly += S(22); }
        foreach (var re in b.ActiveReactions) { Label(R(S(18), ly, S(290), S(22)), Col("#FF9AE6", "✶ " + re)); ly += S(22); }
        Small(R(S(18), ly, S(290), S(20)), Col("#808080", $"Rerolls {run.Rerolls}  Banish {run.Banishes}  Skip {run.Skips}  Revives {run.RevivesLeft}"));
        if (!string.IsNullOrEmpty(h.LastWarning)) Small(R(S(18), ly + S(18), S(290), S(40)), Col("#FF6060", h.LastWarning));
        Small(R(w - S(220), Screen.height - S(48), S(210), S(20)), Col("#707070", $"enemies {h.LiveEnemies}  pickups {h.LivePickups}  [P] pause"));
    }

    // ------------------------------------------------------------------ level up / cache

    private void DrawChoice(RunHost h)
    {
        var run = h.Run; var c = run.Choice; bool shapes = _profile().Settings.ShowRarityShapes;
        Fill(R(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));
        float cw = S(260), ch = S(300), gap = S(18);
        float total = c.Cards.Count * cw + (c.Cards.Count - 1) * gap;
        float x0 = Screen.width / 2f - total / 2f, y0 = Screen.height / 2f - ch / 2f + S(30);
        Title(R(0, y0 - S(110), Screen.width, S(40)), c.IsCache ? $"Cache Opened · {c.Picks} pick{(c.Picks > 1 ? "s" : "")} left" : $"Level {run.Level}");
        if (_banishMode) Center(R(0, y0 - S(70), Screen.width, S(26)), Col("#FF6060", "BANISH: click a card to remove it from this run"));

        if (c.Relic != null)
        {
            var rr = R(Screen.width / 2f - S(330), y0 - S(260), S(660), S(140)); Panel(rr);
            run.Build.Relics.TryGetValue(c.Relic.Slot, out var cur);
            Label(R(rr.x + S(12), rr.y + S(8), S(310), S(24)), "Relic found: " + RarityTag(c.Relic.Rarity, shapes) + " " + c.Relic.Name);
            Small(R(rr.x + S(12), rr.y + S(34), S(310), S(100)), c.Relic.Describe());
            Small(R(rr.x + S(330), rr.y + S(8), S(320), S(90)), cur == null ? Col("#808080", $"{c.Relic.Slot} slot is empty") : "Currently: " + cur.Name + "\n" + cur.Describe());
            if (Button(R(rr.x + S(330), rr.yMax - S(36), S(150), S(28)), "Equip")) run.ResolveRelic(true);
            if (Button(R(rr.x + S(490), rr.yMax - S(36), S(150), S(28)), "Salvage")) run.ResolveRelic(false);
        }

        for (int i = 0; i < c.Cards.Count; i++)
        {
            var card = c.Cards[i];
            var r = R(x0 + i * (cw + gap), y0, cw, ch);
            Panel(r, 0.95f);
            var col = Rarities.Colour[(int)card.Rarity];
            ColorUtility.TryParseHtmlString(col, out var cc);
            Border(r, cc, S(3));
            Small(R(r.x + S(10), r.y + S(8), cw - S(20), S(20)), RarityTag(card.Rarity, shapes) + "  " + Col("#909090", KindText(card.Kind)));
            Title(R(r.x + S(8), r.y + S(34), cw - S(16), S(64)), card.Title);
            Label(R(r.x + S(14), r.y + S(110), cw - S(28), ch - S(160)), card.Text);
            Small(R(r.x + S(10), r.yMax - S(28), cw - S(20), S(20)), Col("#707070", $"[{i + 1}]"));
            if (GUI.Button(r, "", GUIStyle.none) || KeyPressed(KeyCode.Alpha1 + i))
            {
                if (_banishMode) { if (run.Banish(i)) _banishMode = false; else Toast("That card cannot be banished"); }
                else { run.Choose(i); _banishMode = false; }
                return;
            }
        }
        float by = y0 + ch + S(20), bx = Screen.width / 2f - S(240);
        if (run.Rerolls > 0 && Button(R(bx, by, S(150), S(34)), $"Reroll ({run.Rerolls})")) run.Reroll();
        if (run.Banishes > 0 && Button(R(bx + S(165), by, S(150), S(34)), _banishMode ? "Cancel Banish" : $"Banish ({run.Banishes})")) _banishMode = !_banishMode;
        if (!c.IsCache && run.Skips > 0 && Button(R(bx + S(330), by, S(150), S(34)), $"Skip ({run.Skips})")) run.Skip();
    }

    private static string KindText(CardKind k) => k switch
    {
        CardKind.NewSkill => "NEW SKILL", CardKind.SkillRank => "SKILL", CardKind.NewGlyph => "NEW GLYPH",
        CardKind.GlyphRank => "GLYPH", CardKind.Ascend => "ASCENSION", CardKind.Heal => "RESTORE", _ => "REWARD",
    };

    private void DrawVictory(RunHost h)
    {
        Fill(R(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
        var r = R(Screen.width / 2f - S(300), Screen.height / 2f - S(130), S(600), S(260)); Panel(r);
        Big(R(r.x, r.y + S(20), r.width, S(60)), Col("#FF8A2B", "VICTORY"));
        Center(R(r.x, r.y + S(90), r.width, S(50)), "The Harbinger of the Epoch is defeated.\nPress on into Overtime for greater rewards, or claim them now.");
        if (Button(R(r.x + S(40), r.yMax - S(70), S(240), S(44)), "Continue into Overtime")) h.ContinueOvertime();
        if (Button(R(r.xMax - S(280), r.yMax - S(70), S(240), S(44)), "Claim Rewards")) h.Finish();
    }

    private void DrawPause(RunHost h)
    {
        Fill(R(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.5f));
        var r = R(Screen.width / 2f - S(200), Screen.height / 2f - S(110), S(400), S(220)); Panel(r);
        Title(R(r.x, r.y + S(14), r.width, S(40)), "Paused");
        if (Button(R(r.x + S(60), r.y + S(70), S(280), S(40)), "Resume")) { PauseOpen = false; h.Run.SetPaused(false); UnityEngine.Time.timeScale = 1f; }
        if (Button(R(r.x + S(60), r.y + S(125), S(280), S(40)), "Abandon Run")) { PauseOpen = false; h.Abandon(); }
    }

    private void DrawResults(RunHost h)
    {
        var res = h.Results;
        Fill(R(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.7f));
        var r = R(Screen.width / 2f - S(380), Screen.height / 2f - S(300), S(760), S(600)); Panel(r);
        string head = res.Outcome switch { RunState.Victory => Col("#FF8A2B", "VICTORY"), RunState.Defeat => Col("#FF5050", "DEFEATED"), _ => Col("#B0B0B0", "RUN ABANDONED") };
        Big(R(r.x, r.y + S(14), r.width, S(56)), head);
        float y = r.y + S(84);
        Label(R(r.x + S(30), y, S(340), S(24)), $"Time survived  {Gui.Time(res.Time)}"); Label(R(r.x + S(390), y, S(340), S(24)), $"Epoch {ToRoman(res.Tier)}   Corruption {res.Corruption}"); y += S(26);
        Label(R(r.x + S(30), y, S(340), S(24)), $"Level reached  {res.Level}"); Label(R(r.x + S(390), y, S(340), S(24)), $"Enemies slain  {res.Kills}"); y += S(34);
        Label(R(r.x + S(30), y, S(700), S(24)), Col("#E7C46B", $"Time Shards earned: {res.Shards}")); y += S(24);
        Small(R(r.x + S(30), y, S(700), S(40)), string.Join("   ", res.ShardBreakdown.Select(kv => $"{kv.Key} {kv.Value}"))); y += S(36);
        Label(R(r.x + S(30), y, S(700), S(24)), "Skills"); y += S(24);
        double total = Math.Max(1, res.SkillDamage.Sum(s => s.Casts));
        foreach (var (skill, dmg, casts) in res.SkillDamage)
        {
            Bar(R(r.x + S(30), y + S(4), S(400), S(14)), (float)(casts / total), new Color(0.8f, 0.55f, 0.25f), new Color(1, 1, 1, 0.08f));
            Small(R(r.x + S(440), y, S(300), S(22)), $"{skill}  · {casts} casts"); y += S(22);
        }
        y += S(10);
        Small(R(r.x + S(30), y, S(700), S(80)), Col("#A0A0A0", "Build: " + res.Build));
        if (Button(R(r.x + r.width / 2 - S(120), r.yMax - S(60), S(240), S(42)), "Return"))
        { h.Cleanup(); MenuOpen = true; }
    }

    // ------------------------------------------------------------------ menu

    private static readonly string[] Tabs = { "Play", "Obelisk", "Records", "Settings" };

    private void DrawMenu()
    {
        var p = _profile();
        var r = R(Screen.width / 2f - S(460), Screen.height / 2f - S(330), S(920), S(660)); Panel(r, 0.94f);
        Title(R(r.x, r.y + S(10), r.width, S(40)), Col("#E7C46B", "LAST EPOCH · SURVIVORS"));
        Small(R(r.x + S(20), r.y + S(14), S(300), S(20)), Col("#808080", $"Time Shards: {p.Shards}"));
        if (Button(R(r.xMax - S(44), r.y + S(10), S(34), S(30)), "X")) MenuOpen = false;
        for (int i = 0; i < Tabs.Length; i++)
            if (Button(R(r.x + S(20) + i * S(150), r.y + S(60), S(140), S(32)), (_tab == i ? "▸ " : "") + Tabs[i])) _tab = i;
        var body = R(r.x + S(20), r.y + S(110), r.width - S(40), r.height - S(130));
        switch (_tab)
        {
            case 0: DrawPlay(body, p); break;
            case 1: DrawObelisk(body, p); break;
            case 2: DrawRecords(body, p); break;
            case 3: DrawSettings(body, p); break;
        }
    }

    private void DrawPlay(Rect b, Profile p)
    {
        float y = b.y;
        bool inGame = LE.InGame, offline = LE.IsOfflineCharacter;
        Label(R(b.x, y, b.width, S(24)), inGame ? $"Character: {LE.CharacterName}  ·  {LE.PlayerClass}" : Col("#FF6060", "Load into the game with a character first.")); y += S(28);
        if (inGame && !offline) { Label(R(b.x, y, b.width, S(48)), Col("#FF6060", "Survivors mode is offline-only. Switch to an offline character to play.")); y += S(50); }
        if (inGame && LE.IsHardcore) { Small(R(b.x, y, b.width, S(40)), Col("#E7C46B", "Hardcore character: runs never cause a real death. Defeat only ends the run.")); y += S(34); }
        Small(R(b.x, y, b.width, S(40)), Col("#A0A0A0", "The run takes place where you stand, so pick open ground (the Arena of Memory is ideal). Your character's level, gear and XP are not changed by a run.")); y += S(44);

        _tier = Mathf.Clamp(_tier, 1, p.MaxSelectableTier);
        Label(R(b.x, y, S(200), S(30)), $"Epoch tier: {ToRoman(_tier)}");
        if (Button(R(b.x + S(200), y, S(40), S(30)), "-")) _tier = Math.Max(1, _tier - 1);
        if (Button(R(b.x + S(250), y, S(40), S(30)), "+")) _tier = Math.Min(p.MaxSelectableTier, _tier + 1);
        Small(R(b.x + S(310), y + S(4), S(500), S(24)), Col("#808080", $"Highest cleared: {p.HighestTierCleared}  ·  rewards ×{1 + 0.15f * (_tier - 1):0.00}")); y += S(44);

        if (p.OmensUnlocked)
        {
            Label(R(b.x, y, b.width, S(24)), $"Omens (Corruption {OmenCatalog.Corruption(_omens)})"); y += S(28);
            int i = 0;
            foreach (var o in OmenCatalog.All)
            {
                var cr = R(b.x + (i % 2) * (b.width / 2), y + (i / 2) * S(30), b.width / 2 - S(10), S(28));
                bool on = _omens.Contains(o.Id);
                if (Button(cr, $"{(on ? "☑" : "☐")} {o.Name} (+{o.Corruption}): {o.Text}")) { if (on) _omens.Remove(o.Id); else _omens.Add(o.Id); }
                i++;
            }
            y += ((OmenCatalog.All.Length + 1) / 2) * S(30) + S(10);
        }
        else { Small(R(b.x, y, b.width, S(24)), Col("#707070", "Omens are locked. Unlock the Epoch Gate in the Obelisk.")); y += S(30); }

        bool can = inGame && offline;
        if (Button(R(b.x + b.width / 2 - S(150), b.yMax - S(60), S(300), S(50)), can ? "BEGIN RUN" : "Unavailable") && can)
        {
            MenuOpen = false;
            _start(new RunConfig(LE.PlayerClass, _tier, _omens.ToList(), (ulong)DateTime.UtcNow.Ticks, p.Settings.EnemyCap));
        }
    }

    private Vector2 _scroll;
    private void DrawObelisk(Rect b, Profile p)
    {
        Small(R(b.x, b.y, b.width, S(22)), Col("#A0A0A0", "Spend Time Shards on permanent upgrades. Respec refunds everything."));
        if (Button(R(b.xMax - S(120), b.y - S(4), S(120), S(28)), "Respec")) { p.Respec(); _save(); }
        float rowH = S(30); int cols = 2; float colW = b.width / cols;
        int i = 0;
        foreach (var n in MetaCatalog.All)
        {
            var rr = R(b.x + (i % cols) * colW, b.y + S(32) + (i / cols) * rowH, colW - S(10), rowH - S(4));
            int rank = p.Rank(n.Id);
            bool can = p.CanBuy(n.Id, out var why);
            string cost = rank >= n.MaxRank ? "MAX" : n.CostOf(rank).ToString();
            Fill(rr, new Color(1, 1, 1, rank > 0 ? 0.07f : 0.03f));
            Small(R(rr.x + S(6), rr.y + S(5), colW - S(150), S(22)), $"{Col(rank > 0 ? "#E7C46B" : "#C0C0C0", n.Name)} {rank}/{n.MaxRank}: {Col("#909090", n.Text)}");
            if (Button(R(rr.xMax - S(110), rr.y + S(1), S(106), rr.height - S(2)), can ? $"Buy {cost}" : (rank >= n.MaxRank ? "MAX" : cost)))
            { if (p.Buy(n.Id)) _save(); else Toast(why); }
            i++;
        }
    }

    private void DrawRecords(Rect b, Profile p)
    {
        float y = b.y;
        Label(R(b.x, y, b.width, S(24)), $"Runs started {p.RunsStarted}  ·  won {p.RunsWon}  ·  enemies slain {p.TotalKills}  ·  lifetime shards {p.LifetimeShards}"); y += S(36);
        foreach (CharacterClass c in Enum.GetValues(typeof(CharacterClass)))
        {
            p.Records.TryGetValue(c.ToString(), out var rec);
            Label(R(b.x, y, b.width, S(24)), rec == null ? Col("#707070", $"{c}: no runs yet")
                : $"{c}: best time {Gui.Time(rec.BestTime)} · highest Epoch won {ToRoman(Math.Max(1, rec.BestTier))}{(rec.BestTier == 0 ? " (none)" : "")} · best kills {rec.BestKills} · best level {rec.BestLevel}");
            y += S(28);
        }
    }

    private void DrawSettings(Rect b, Profile p)
    {
        var s = p.Settings; float y = b.y; bool changed = false;
        Label(R(b.x, y, S(300), S(30)), $"Enemy cap (performance): {s.EnemyCap}");
        if (Button(R(b.x + S(320), y, S(40), S(28)), "-")) { s.EnemyCap = Math.Max(80, s.EnemyCap - 20); changed = true; }
        if (Button(R(b.x + S(370), y, S(40), S(28)), "+")) { s.EnemyCap = Math.Min(500, s.EnemyCap + 20); changed = true; }
        y += S(38);
        Label(R(b.x, y, S(300), S(30)), $"Targeting: {s.Targeting}");
        if (Button(R(b.x + S(320), y, S(140), S(28)), "Change")) { s.Targeting = (TargetingOverride)(((int)s.Targeting + 1) % 3); changed = true; }
        y += S(38);
        if (Button(R(b.x, y, S(420), S(30)), $"{(s.PauseOnLevelUp ? "☑" : "☐")} Pause the game while choosing upgrades")) { s.PauseOnLevelUp = !s.PauseOnLevelUp; changed = true; }
        y += S(38);
        if (Button(R(b.x, y, S(420), S(30)), $"{(s.ShowRarityShapes ? "☑" : "☐")} Show rarity shapes (colour-blind aid)")) { s.ShowRarityShapes = !s.ShowRarityShapes; changed = true; }
        y += S(38);
        Label(R(b.x, y, S(300), S(30)), $"Interface scale: {s.UiScale:0.0}");
        if (Button(R(b.x + S(320), y, S(40), S(28)), "-")) { s.UiScale = Mathf.Max(0.7f, s.UiScale - 0.1f); changed = true; }
        if (Button(R(b.x + S(370), y, S(40), S(28)), "+")) { s.UiScale = Mathf.Min(2f, s.UiScale + 0.1f); changed = true; }
        y += S(44);
        Small(R(b.x, y, b.width, S(60)), Col("#909090", $"Keys: {s.MenuKey} opens this menu · {s.PauseKey} pauses a run · 1-4 pick an upgrade card"));
        if (changed) _save();
    }

    // ------------------------------------------------------------------ helpers

    private static readonly HashSet<KeyCode> _keysThisFrame = new();
    public static void FeedKey(KeyCode k) => _keysThisFrame.Add(k);
    public static void EndFrame() => _keysThisFrame.Clear();
    private static bool KeyPressed(KeyCode k) => _keysThisFrame.Contains(k);

    public static string ToRoman(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", _ => n.ToString(),
    };
}
