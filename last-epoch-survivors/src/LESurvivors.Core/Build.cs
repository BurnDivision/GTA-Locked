using LESurvivors.Core.Content;

namespace LESurvivors.Core;

public sealed class OwnedSkill
{
    public SkillDef Def { get; }
    public int Rank { get; internal set; } = 1;
    public bool Ascended { get; internal set; }
    public int MulticastPicks { get; internal set; }
    public StatSheet Local { get; } = new();   // per-skill stats from rank-ups
    public double DamageDealt { get; set; }    // filled by the adapter for the results screen
    public int Casts { get; set; }
    public OwnedSkill(SkillDef def) { Def = def; }
}

public sealed class OwnedGlyph
{
    public GlyphDef Def { get; }
    public int Rank { get; internal set; }
    public StatSheet Stats { get; } = new();
    public OwnedGlyph(GlyphDef def) { Def = def; }
}

/// <summary>The player's build for one run and the derived stat sheets.</summary>
public sealed class Build
{
    public const int MaxSkillRank = 10;
    public const int MaxGlyphs = 6;
    public const int MaxMulticastPicks = 3;
    public const float AscensionMore = 1.75f;

    public List<OwnedSkill> Skills { get; } = new();
    public List<OwnedGlyph> Glyphs { get; } = new();
    public Dictionary<RelicSlot, Relic> Relics { get; } = new();
    public StatSheet Base { get; } = new();          // meta + class + level scaling
    public StatSheet Global { get; private set; } = new();
    public Dictionary<Tag, int> TagCounts { get; } = new();
    public Dictionary<Tag, int> ResonanceTier { get; } = new();  // 0..3
    public HashSet<Proc> ActiveProcs { get; } = new();
    public List<string> ActiveReactions { get; } = new();
    public int ActiveSlots { get; set; } = MetaCatalog.BaseActiveSlots;
    public int RelicSlots { get; set; } = 3;

    public OwnedSkill? Skill(string id) => Skills.FirstOrDefault(s => s.Def.Id == id);
    public OwnedGlyph? Glyph(string id) => Glyphs.FirstOrDefault(g => g.Def.Id == id);

    public OwnedSkill AddSkill(SkillDef def)
    {
        if (Skill(def.Id) is { } existing) return existing;
        if (Skills.Count >= ActiveSlots) throw new InvalidOperationException("No free skill slot");
        var s = new OwnedSkill(def); Skills.Add(s); Recompute(); return s;
    }

    public void RankSkill(string id, SkillLine line, Rarity r)
    {
        var s = Skill(id) ?? throw new InvalidOperationException($"Skill {id} not owned");
        if (s.Rank >= MaxSkillRank) throw new InvalidOperationException("Skill at max rank");
        s.Rank++;
        if (line == SkillLine.Multicast) s.MulticastPicks++;
        s.Local.Add(SkillLines.Mod(line, r, s.Def.PrimaryDamage));
        Recompute();
    }

    public bool IsAscensionReady(OwnedSkill s)
        => !s.Ascended && s.Rank >= MaxSkillRank && ResonanceTier.GetValueOrDefault(s.Def.PrimaryDamage) >= 2;

    public void Ascend(string id)
    {
        var s = Skill(id) ?? throw new InvalidOperationException();
        if (!IsAscensionReady(s)) throw new InvalidOperationException("Not ascension-ready");
        s.Ascended = true;
        s.Local.Add(new StatMod(Stat.Multicast, 1));
        s.Local.Add(new StatMod(Stat.IncreasedArea, 25));
        Recompute();
    }

    public OwnedGlyph AddOrRankGlyph(GlyphDef def, Rarity r)
    {
        var g = Glyph(def.Id);
        if (g == null)
        {
            if (Glyphs.Count >= MaxGlyphs) throw new InvalidOperationException("No free glyph slot");
            g = new OwnedGlyph(def); Glyphs.Add(g);
        }
        if (g.Rank >= def.MaxRank) throw new InvalidOperationException("Glyph at max rank");
        g.Rank++;
        foreach (var m in def.PerRank) g.Stats.Add(m.Scaled(r.Mag()));
        Recompute();
        return g;
    }

    /// <summary>Equips a relic, returning the one it replaced (if any).</summary>
    public Relic? EquipRelic(Relic relic)
    {
        Relics.TryGetValue(relic.Slot, out var old);
        Relics[relic.Slot] = relic;
        Recompute();
        return old;
    }

    public bool HasPower(RelicPower p) => Relics.Values.Any(r => r.Power == p);

    public void Recompute()
    {
        TagCounts.Clear();
        foreach (var s in Skills) foreach (var t in Tags.Split(s.Def.Tags)) TagCounts[t] = TagCounts.GetValueOrDefault(t) + 1;
        foreach (var g in Glyphs) foreach (var t in Tags.Split(g.Def.Tags)) TagCounts[t] = TagCounts.GetValueOrDefault(t) + 1;

        var sheet = new StatSheet();
        sheet.Add(Base);
        foreach (var g in Glyphs) sheet.Add(g.Stats);
        foreach (var r in Relics.Values) sheet.Add(r.Mods);

        ResonanceTier.Clear(); ActiveProcs.Clear(); ActiveReactions.Clear();
        foreach (var (tag, tiers) in ResonanceCatalog.Tiers)
        {
            int count = TagCounts.GetValueOrDefault(tag), tier = 0;
            foreach (var t in tiers)
            {
                if (count < t.Need) break;
                tier++; sheet.Add(t.Stats);
                if (t.Proc != Proc.None) ActiveProcs.Add(t.Proc);
            }
            if (tier > 0) ResonanceTier[tag] = tier;
        }
        foreach (var (a, b, proc, name, _) in ResonanceCatalog.Reactions)
            if (ResonanceTier.GetValueOrDefault(a) >= 2 && ResonanceTier.GetValueOrDefault(b) >= 2 && ActiveProcs.Add(proc))
                ActiveReactions.Add(name);
        Global = sheet;
    }

    /// <summary>Effective stat for one skill: its own rank-up stats plus global stats that match its tags.</summary>
    public float For(OwnedSkill s, Stat stat) => s.Local.Get(stat) + Global.GetFor(stat, s.Def.Tags);

    /// <summary>Seconds between casts.</summary>
    public float IntervalOf(OwnedSkill s)
    {
        float freq = For(s, Stat.CastFrequency) / 100f;
        return Math.Max(0.12f, s.Def.BaseInterval / (1f + Math.Max(-0.5f, freq)));
    }

    public int CastsPerTrigger(OwnedSkill s) => 1 + (int)Math.Round(For(s, Stat.Multicast));

    /// <summary>Multiplier to LE's own damage for this skill: (1+increased) * Π(1+more).</summary>
    public float DamageMultiplier(OwnedSkill s)
    {
        float inc = For(s, Stat.IncreasedDamage) / 100f;
        float more = 1f + For(s, Stat.MoreDamage) / 100f;
        return (1f + inc) * more * (s.Ascended ? AscensionMore : 1f);
    }

    public string Summary()
    {
        var parts = Skills.Select(s => $"{s.Def.Name} {(s.Ascended ? "★" : "")}{s.Rank}")
            .Concat(Glyphs.Select(g => $"{g.Def.Name} {g.Rank}"));
        return string.Join(", ", parts);
    }
}
