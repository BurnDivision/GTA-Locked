namespace LESurvivors.Core;

/// <summary>
/// Run stats. The first group maps onto Last Epoch <c>SP</c> stats (applied through StatBuffs or per-cast temp stats);
/// the second group is mod-only and consumed by the mod's own systems.
/// </summary>
public enum Stat
{
    // ---- mapped to LE stats ----
    IncreasedDamage,     // SP.Damage (increased), tag-scoped
    MoreDamage,          // SP.Damage (more multiplier), tag-scoped
    IncreasedArea,       // SP.IncreasedAreaForAreaSkills
    CritChance,          // SP.CriticalChance (added, percent points)
    CritMulti,           // SP.CriticalMultiplier (added, percent points)
    IgniteChance, ChillChance, ShockChance, BleedChance, PoisonChance, FreezeRate, // SP.AilmentChance per ailment
    AilmentEffect,       // SP.IncreasedAilmentEffect
    MoveSpeed,           // SP.Movespeed (increased)
    MaxHealthPct,        // SP.Health (increased)
    AddedHealth,         // SP.Health (added)
    HealthRegen,         // SP.HealthRegen (added per second)
    Armour,              // SP.Armour (added)
    AllResistance,       // SP.AllResistances (added)
    DamageReduction,     // SP.DamageTaken (negative increased)
    Penetration,         // SP.Penetration
    // ---- mod-only ----
    CastFrequency,       // casts per second multiplier: interval / (1 + x)
    Multicast,           // extra casts per trigger
    PickupRadius,        // increased %
    Luck,                // flat points
    XpGain,              // increased %
    ShardGain,           // increased %
    Revives,             // flat
    Rerolls, Banishes, Skips, // flat charges
    ExtraChoices,        // flat extra level-up cards
    HealingEffect,       // increased % (health orbs)
}

/// <summary>A stat value scoped to a set of ability tags (Tag.None = global).</summary>
public readonly record struct StatMod(Stat Stat, float Value, Tag Scope = Tag.None)
{
    public StatMod Scaled(float k) => this with { Value = Value * k };
    public override string ToString() => StatText.Describe(this);
}

/// <summary>Accumulates StatMods. Values add within the same (Stat, Scope) key.</summary>
public sealed class StatSheet
{
    private readonly Dictionary<(Stat, Tag), float> _v = new();
    public IReadOnlyDictionary<(Stat, Tag), float> Values => _v;

    public void Add(StatMod m) { _v.TryGetValue((m.Stat, m.Scope), out var c); _v[(m.Stat, m.Scope)] = c + m.Value; }
    public void Add(IEnumerable<StatMod> ms) { foreach (var m in ms) Add(m); }
    public void Add(StatSheet other) { foreach (var kv in other._v) Add(new StatMod(kv.Key.Item1, kv.Value, kv.Key.Item2)); }

    /// <summary>Global value only.</summary>
    public float Get(Stat s) => _v.TryGetValue((s, Tag.None), out var v) ? v : 0f;

    /// <summary>Global value plus every tag-scoped value whose scope intersects <paramref name="tags"/>.</summary>
    public float GetFor(Stat s, Tag tags)
    {
        float total = 0;
        foreach (var kv in _v)
            if (kv.Key.Item1 == s && (kv.Key.Item2 == Tag.None || (kv.Key.Item2 & tags) != 0)) total += kv.Value;
        return total;
    }

    public IEnumerable<StatMod> All() => _v.Select(kv => new StatMod(kv.Key.Item1, kv.Value, kv.Key.Item2));
    public void Clear() => _v.Clear();
}

public static class StatText
{
    private static bool IsPercent(Stat s) => s switch
    {
        Stat.AddedHealth or Stat.HealthRegen or Stat.Armour or Stat.Luck or Stat.Multicast or Stat.Revives
            or Stat.Rerolls or Stat.Banishes or Stat.Skips or Stat.ExtraChoices => false,
        _ => true,
    };

    public static string Label(Stat s) => s switch
    {
        Stat.IncreasedDamage => "increased Damage",
        Stat.MoreDamage => "more Damage",
        Stat.IncreasedArea => "increased Area",
        Stat.CritChance => "Critical Strike Chance",
        Stat.CritMulti => "Critical Strike Multiplier",
        Stat.IgniteChance => "chance to Ignite",
        Stat.ChillChance => "chance to Chill",
        Stat.ShockChance => "chance to Shock",
        Stat.BleedChance => "chance to inflict Bleed",
        Stat.PoisonChance => "chance to Poison",
        Stat.FreezeRate => "increased Freeze Rate",
        Stat.AilmentEffect => "increased Ailment Effect",
        Stat.MoveSpeed => "increased Movement Speed",
        Stat.MaxHealthPct => "increased Health",
        Stat.AddedHealth => "Health",
        Stat.HealthRegen => "Health Regen",
        Stat.Armour => "Armour",
        Stat.AllResistance => "All Resistances",
        Stat.DamageReduction => "less Damage Taken",
        Stat.Penetration => "Penetration",
        Stat.CastFrequency => "Cast Frequency",
        Stat.Multicast => "Multicast",
        Stat.PickupRadius => "Pickup Radius",
        Stat.Luck => "Luck",
        Stat.XpGain => "increased Experience",
        Stat.ShardGain => "increased Time Shards",
        Stat.Revives => "Revive",
        Stat.Rerolls => "Reroll",
        Stat.Banishes => "Banish",
        Stat.Skips => "Skip",
        Stat.ExtraChoices => "extra level-up choice",
        Stat.HealingEffect => "increased Healing",
        _ => s.ToString(),
    };

    public static string Describe(StatMod m)
    {
        var scope = m.Scope == Tag.None ? "" : " (" + string.Join("/", Tags.Split(m.Scope)) + ")";
        var num = IsPercent(m.Stat) ? $"{m.Value:+0.#;-0.#}%" : $"{m.Value:+0.#;-0.#}";
        return $"{num} {Label(m.Stat)}{scope}";
    }
}
