namespace LESurvivors.Core.Content;

/// <summary>A passive. <see cref="PerRank"/> is the Normal-rarity gain per rank; rarity scales each pick.</summary>
public sealed record GlyphDef(string Id, string Name, Tag Tags, StatMod[] PerRank, int MaxRank = 5)
{
    public string Describe(Rarity r) => string.Join(", ", PerRank.Select(m => m.Scaled(r.Mag()).ToString()));
}

public static class GlyphCatalog
{
    public static readonly GlyphDef[] All =
    {
        new("embers", "Glyph of Embers", Tag.Fire, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Fire), new StatMod(Stat.IgniteChance, 8) }),
        new("rime", "Glyph of Rime", Tag.Cold, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Cold), new StatMod(Stat.ChillChance, 10) }),
        new("storm", "Glyph of the Storm", Tag.Lightning, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Lightning), new StatMod(Stat.ShockChance, 8) }),
        new("iron", "Glyph of Iron", Tag.Physical, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Physical), new StatMod(Stat.BleedChance, 8) }),
        new("abyss", "Glyph of the Abyss", Tag.Void, new[] { new StatMod(Stat.IncreasedDamage, 12, Tag.Void), new StatMod(Stat.Penetration, 2, Tag.Void) }),
        new("grave", "Glyph of the Grave", Tag.Necrotic, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Necrotic), new StatMod(Stat.AilmentEffect, 5) }),
        new("venom", "Glyph of Venom", Tag.Poison, new[] { new StatMod(Stat.IncreasedDamage, 8, Tag.Poison), new StatMod(Stat.PoisonChance, 12) }),
        new("arcana", "Glyph of Arcana", Tag.Spell, new[] { new StatMod(Stat.IncreasedDamage, 8, Tag.Spell), new StatMod(Stat.CastFrequency, 3) }),
        new("blades", "Glyph of Blades", Tag.Melee, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.Melee), new StatMod(Stat.IncreasedArea, 4, Tag.Melee) }),
        new("flight", "Glyph of Flight", Tag.Throwing | Tag.Bow, new[] { new StatMod(Stat.IncreasedDamage, 9, Tag.Throwing | Tag.Bow), new StatMod(Stat.CritChance, 1.5f) }),
        new("rot", "Glyph of Rot", Tag.DoT, new[] { new StatMod(Stat.IncreasedDamage, 10, Tag.DoT), new StatMod(Stat.AilmentEffect, 6) }),
        new("haste", "Glyph of Haste", Tag.None, new[] { new StatMod(Stat.MoveSpeed, 6) }),
        new("vitality", "Glyph of Vitality", Tag.None, new[] { new StatMod(Stat.MaxHealthPct, 8), new StatMod(Stat.HealthRegen, 2) }),
        new("reach", "Glyph of Reach", Tag.None, new[] { new StatMod(Stat.PickupRadius, 20) }),
        new("fortune", "Glyph of Fortune", Tag.None, new[] { new StatMod(Stat.Luck, 8), new StatMod(Stat.XpGain, 5) }),
        new("precision", "Glyph of Precision", Tag.None, new[] { new StatMod(Stat.CritChance, 2.5f), new StatMod(Stat.CritMulti, 12) }),
        new("celerity", "Glyph of Celerity", Tag.None, new[] { new StatMod(Stat.CastFrequency, 6) }),
        new("bulwark", "Glyph of the Bulwark", Tag.None, new[] { new StatMod(Stat.Armour, 60), new StatMod(Stat.AllResistance, 5) }),
        new("expanse", "Glyph of Expanse", Tag.None, new[] { new StatMod(Stat.IncreasedArea, 7) }),
    };

    public static GlyphDef Get(string id) => All.First(g => g.Id == id);
}
