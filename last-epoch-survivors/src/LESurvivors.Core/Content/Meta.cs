namespace LESurvivors.Core.Content;

/// <summary>A node in the Obelisk (meta tree). Cost of rank r (0-based) = BaseCost * (1 + r * CostGrowth).</summary>
public sealed record MetaNode(string Id, string Name, string Text, int MaxRank, int BaseCost, float CostGrowth,
    StatMod[] PerRank, string? Requires = null, int RequiresRank = 1)
{
    public int CostOf(int currentRank) => (int)Math.Round(BaseCost * (1 + currentRank * CostGrowth));
}

public static class MetaCatalog
{
    private static StatMod S(Stat s, float v) => new(s, v);
    private static readonly StatMod[] None = Array.Empty<StatMod>();

    public static readonly MetaNode[] All =
    {
        new("vitality", "Vitality", "+6% maximum health", 5, 40, 0.6f, new[] { S(Stat.MaxHealthPct, 6) }),
        new("potency", "Potency", "+5% damage", 5, 50, 0.6f, new[] { S(Stat.IncreasedDamage, 5) }),
        new("insight", "Insight", "+5% experience", 5, 40, 0.5f, new[] { S(Stat.XpGain, 5) }),
        new("reach", "Reach", "+15% pickup radius", 3, 30, 0.5f, new[] { S(Stat.PickupRadius, 15) }),
        new("swiftness", "Swiftness", "+3% movement speed", 3, 40, 0.6f, new[] { S(Stat.MoveSpeed, 3) }),
        new("fortune", "Fortune", "+5 luck", 5, 60, 0.6f, new[] { S(Stat.Luck, 5) }),
        new("celerity", "Celerity", "+3% cast frequency", 5, 60, 0.7f, new[] { S(Stat.CastFrequency, 3) }),
        new("ward", "Warding", "+3% all resistances, +40 armour", 5, 40, 0.5f, new[] { S(Stat.AllResistance, 3), S(Stat.Armour, 40) }),
        new("regrowth", "Regrowth", "+1.5 health regen", 3, 40, 0.5f, new[] { S(Stat.HealthRegen, 1.5f) }),
        new("greed", "Greed", "+8% time shards", 5, 50, 0.6f, new[] { S(Stat.ShardGain, 8) }),
        new("secondwind", "Second Wind", "+1 revive", 2, 250, 1.0f, new[] { S(Stat.Revives, 1) }, "vitality", 3),
        new("reroll", "Reroll", "+1 reroll per run", 3, 80, 0.8f, new[] { S(Stat.Rerolls, 1) }),
        new("banish", "Banish", "+1 banish per run", 3, 80, 0.8f, new[] { S(Stat.Banishes, 1) }, "reroll"),
        new("skip", "Skip", "+1 skip per run", 3, 50, 0.6f, new[] { S(Stat.Skips, 1) }),
        new("foresight", "Foresight", "Offer 4 choices on level-up", 1, 600, 0, new[] { S(Stat.ExtraChoices, 1) }, "fortune", 3),
        new("arsenal", "Arsenal", "Unlock an extra active skill slot", 2, 300, 1.0f, None, "potency", 2),
        new("reliquary", "Reliquary", "Unlock the Idol relic slot", 1, 400, 0, None, "fortune", 1),
        new("epochgate", "Epoch Gate", "Unlock Omens (curses) for bigger rewards", 1, 200, 0, None),
        new("prism", "Prismatic Start", "Start each run with a random Glyph", 1, 350, 0, None, "insight", 2),
        new("echoing", "Echoing Cache", "Caches have +1 upgrade", 1, 500, 0, None, "greed", 2),
    };

    public static MetaNode Get(string id) => All.First(n => n.Id == id);
    public const int BaseActiveSlots = 4;
}
