using LESurvivors.Core.Content;

namespace LESurvivors.Core;

public enum CardKind { NewSkill, SkillRank, NewGlyph, GlyphRank, Ascend, Heal, Shards }

public sealed record Card(CardKind Kind, string SubjectId, string Title, string Text, Rarity Rarity, SkillLine Line = SkillLine.Damage)
{
    public override string ToString() => $"[{Rarity}] {Title}: {Text}";
}

/// <summary>Generates level-up / cache choices.</summary>
public sealed class OfferGenerator
{
    private readonly Build _b;
    private readonly IReadOnlyList<SkillDef> _pool;
    private readonly Rng _rng;
    public HashSet<string> Banished { get; } = new();

    public OfferGenerator(Build build, IReadOnlyList<SkillDef> skillPool, Rng rng)
    { _b = build; _pool = skillPool; _rng = rng; }

    private IEnumerable<(Card card, double weight)> Candidates(float luck, Rarity minRarity)
    {
        // Owned skills: rank-ups
        foreach (var s in _b.Skills)
        {
            if (Banished.Contains(s.Def.Id)) continue;
            if (_b.IsAscensionReady(s))
            { yield return (new Card(CardKind.Ascend, s.Def.Id, $"Ascend {s.Def.Name}", "+1 Multicast, 75% more damage, +25% area", Rarity.Legendary), 25); continue; }
            if (s.Rank >= Build.MaxSkillRank) continue;
            var lineWeights = SkillLines.All.Select(l =>
                l == SkillLine.Multicast && s.MulticastPicks >= Build.MaxMulticastPicks ? 0 : SkillLines.Weight(l)).ToList();
            var line = SkillLines.All[_rng.PickWeighted(lineWeights)];
            var min = (Rarity)Math.Max((int)minRarity, (int)SkillLines.MinimumRarity(line));
            var r = Rarities.Roll(_rng, luck, min);
            var mod = SkillLines.Mod(line, r, s.Def.PrimaryDamage);
            yield return (new Card(CardKind.SkillRank, s.Def.Id, $"{s.Def.Name} {s.Rank + 1}", mod.ToString(), r, line), 10 * 1.6);
        }
        // New skills
        if (_b.Skills.Count < _b.ActiveSlots)
            foreach (var d in _pool)
            {
                if (_b.Skill(d.Id) != null || Banished.Contains(d.Id)) continue;
                var tagSynergy = Tags.Split(d.Tags).Sum(t => _b.TagCounts.GetValueOrDefault(t));
                yield return (new Card(CardKind.NewSkill, d.Id, d.Name, "New skill · " + string.Join(" ", Tags.Split(d.Tags)), Rarity.Normal), 6 + tagSynergy * 1.5);
            }
        // Glyphs
        foreach (var g in GlyphCatalog.All)
        {
            if (Banished.Contains(g.Id)) continue;
            var owned = _b.Glyph(g.Id);
            if (owned == null && _b.Glyphs.Count >= Build.MaxGlyphs) continue;
            if (owned != null && owned.Rank >= g.MaxRank) continue;
            var r = Rarities.Roll(_rng, luck, minRarity);
            var synergy = Tags.Split(g.Tags).Sum(t => _b.TagCounts.GetValueOrDefault(t));
            double w = owned != null ? 9 * 1.6 : 5 + synergy * 1.2;
            var kind = owned == null ? CardKind.NewGlyph : CardKind.GlyphRank;
            var title = owned == null ? g.Name : $"{g.Name} {owned.Rank + 1}";
            yield return (new Card(kind, g.Id, title, g.Describe(r), r), w);
        }
    }

    public List<Card> Generate(int count, float luck, Rarity minRarity = Rarity.Normal)
    {
        var cands = Candidates(luck, minRarity).ToList();
        var result = new List<Card>();
        var usedSubjects = new HashSet<string>();
        while (result.Count < count && cands.Count > 0)
        {
            int i = _rng.PickWeighted(cands.Select(c => c.weight).ToList());
            if (i < 0) break;
            var c = cands[i].card;
            cands.RemoveAt(i);
            if (!usedSubjects.Add(c.SubjectId)) continue;
            result.Add(c);
        }
        // Fillers once the build is complete
        while (result.Count < count)
        {
            result.Add(result.Any(c => c.Kind == CardKind.Heal)
                ? new Card(CardKind.Shards, "shards", "Time Shards", "+25 Time Shards", Rarity.Normal)
                : new Card(CardKind.Heal, "heal", "Restoration", "Heal 30% of maximum health", Rarity.Normal));
        }
        return result;
    }

    /// <summary>Applies a chosen card to the build. Returns false for fillers that the run must handle.</summary>
    public bool Apply(Card c)
    {
        switch (c.Kind)
        {
            case CardKind.NewSkill: _b.AddSkill(_pool.First(p => p.Id == c.SubjectId)); return true;
            case CardKind.SkillRank: _b.RankSkill(c.SubjectId, c.Line, c.Rarity); return true;
            case CardKind.NewGlyph:
            case CardKind.GlyphRank: _b.AddOrRankGlyph(GlyphCatalog.Get(c.SubjectId), c.Rarity); return true;
            case CardKind.Ascend: _b.Ascend(c.SubjectId); return true;
            default: return false;
        }
    }
}
