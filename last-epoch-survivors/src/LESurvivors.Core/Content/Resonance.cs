namespace LESurvivors.Core.Content;

/// <summary>Event-driven effects the adapter implements with real LE abilities / ailments.</summary>
public enum Proc
{
    None,
    FireNovaOnIgnitedKill,    // Fire III
    FrostBurstOnFrozenKill,   // Cold III
    ChainLightningOnCrit,     // Lightning III
    BloodBurstOnBleedKill,    // Physical III
    VoidEchoOnHit,            // Void III
    SoulOnKill,               // Necrotic III: kills release a homing soul
    PoisonCloudOnKill,        // Poison III
    // reactions
    Shatter, Overload, Blight, Rift,
}

public sealed record ResonanceTier(int Need, StatMod[] Stats, Proc Proc, string Text);

public static class ResonanceCatalog
{
    public static readonly int[] Thresholds = { 2, 4, 6 };

    private static ResonanceTier[] Element(Tag t, Stat ailment, Proc p3, string ailmentWord, string p3Text) => new[]
    {
        new ResonanceTier(2, new[] { new StatMod(Stat.IncreasedDamage, 20, t), new StatMod(ailment, 10) }, Proc.None, $"+20% {t} damage, +10% {ailmentWord} chance"),
        new ResonanceTier(4, new[] { new StatMod(Stat.MoreDamage, 15, t), new StatMod(Stat.AilmentEffect, 20), new StatMod(ailment, 15) }, Proc.None, $"15% more {t} damage, +20% ailment effect"),
        new ResonanceTier(6, new[] { new StatMod(Stat.MoreDamage, 20, t) }, p3, p3Text),
    };

    public static readonly Dictionary<Tag, ResonanceTier[]> Tiers = new()
    {
        [Tag.Fire] = Element(Tag.Fire, Stat.IgniteChance, Proc.FireNovaOnIgnitedKill, "ignite", "Ignited enemies erupt in a fire nova on death"),
        [Tag.Cold] = Element(Tag.Cold, Stat.ChillChance, Proc.FrostBurstOnFrozenKill, "chill", "Frozen enemies burst into ice shards on death"),
        [Tag.Lightning] = Element(Tag.Lightning, Stat.ShockChance, Proc.ChainLightningOnCrit, "shock", "Critical strikes call chain lightning"),
        [Tag.Physical] = Element(Tag.Physical, Stat.BleedChance, Proc.BloodBurstOnBleedKill, "bleed", "Bleeding enemies burst on death"),
        [Tag.Void] = Element(Tag.Void, Stat.BleedChance, Proc.VoidEchoOnHit, "bleed", "Void hits echo once at 30% damage"),
        [Tag.Necrotic] = Element(Tag.Necrotic, Stat.PoisonChance, Proc.SoulOnKill, "poison", "Kills release a hungering soul"),
        [Tag.Poison] = Element(Tag.Poison, Stat.PoisonChance, Proc.PoisonCloudOnKill, "poison", "Poisoned enemies leave a toxic cloud on death"),
        [Tag.Spell] = new[]
        {
            new ResonanceTier(2, new[] { new StatMod(Stat.CastFrequency, 8, Tag.Spell) }, Proc.None, "+8% spell cast frequency"),
            new ResonanceTier(4, new[] { new StatMod(Stat.IncreasedArea, 15, Tag.Spell), new StatMod(Stat.CastFrequency, 8, Tag.Spell) }, Proc.None, "+15% spell area, +8% frequency"),
            new ResonanceTier(6, new[] { new StatMod(Stat.MoreDamage, 25, Tag.Spell) }, Proc.None, "25% more spell damage"),
        },
        [Tag.Melee] = new[]
        {
            new ResonanceTier(2, new[] { new StatMod(Stat.Armour, 150), new StatMod(Stat.IncreasedDamage, 15, Tag.Melee) }, Proc.None, "+150 armour, +15% melee damage"),
            new ResonanceTier(4, new[] { new StatMod(Stat.DamageReduction, 10), new StatMod(Stat.IncreasedArea, 20, Tag.Melee) }, Proc.None, "10% less damage taken, +20% melee area"),
            new ResonanceTier(6, new[] { new StatMod(Stat.MoreDamage, 30, Tag.Melee) }, Proc.None, "30% more melee damage"),
        },
        [Tag.Throwing] = Delivery(Tag.Throwing), [Tag.Bow] = Delivery(Tag.Bow),
        [Tag.DoT] = new[]
        {
            new ResonanceTier(2, new[] { new StatMod(Stat.AilmentEffect, 15) }, Proc.None, "+15% ailment effect"),
            new ResonanceTier(4, new[] { new StatMod(Stat.IncreasedDamage, 30, Tag.DoT) }, Proc.None, "+30% damage over time"),
            new ResonanceTier(6, new[] { new StatMod(Stat.MoreDamage, 25, Tag.DoT) }, Proc.None, "25% more damage over time"),
        },
        [Tag.Minion] = new[]
        {
            new ResonanceTier(2, new[] { new StatMod(Stat.IncreasedDamage, 20, Tag.Minion) }, Proc.None, "+20% minion damage"),
            new ResonanceTier(4, new[] { new StatMod(Stat.CastFrequency, 15, Tag.Minion) }, Proc.None, "+15% minion skill frequency"),
            new ResonanceTier(6, new[] { new StatMod(Stat.MoreDamage, 30, Tag.Minion) }, Proc.None, "30% more minion damage"),
        },
    };

    private static ResonanceTier[] Delivery(Tag t) => new[]
    {
        new ResonanceTier(2, new[] { new StatMod(Stat.CritChance, 4, t) }, Proc.None, $"+4% {t} crit chance"),
        new ResonanceTier(4, new[] { new StatMod(Stat.CritMulti, 40, t), new StatMod(Stat.CastFrequency, 8, t) }, Proc.None, $"+40% {t} crit multiplier"),
        new ResonanceTier(6, new[] { new StatMod(Stat.Multicast, 1, t) }, Proc.None, $"+1 multicast for {t} skills"),
    };

    /// <summary>Cross-element reactions: active when both tags have Resonance II.</summary>
    public static readonly (Tag A, Tag B, Proc Proc, string Name, string Text)[] Reactions =
    {
        (Tag.Cold, Tag.Physical, Proc.Shatter, "Shatter", "Frozen enemies explode for 30% of their max health"),
        (Tag.Lightning, Tag.Fire, Proc.Overload, "Overload", "Shock lasts twice as long and shocked enemies spread ignite"),
        (Tag.Poison, Tag.Necrotic, Proc.Blight, "Blight", "Poison stacks reduce necrotic resistance"),
        (Tag.Void, Tag.Fire, Proc.Rift, "Rift", "Void hits echo at 30% damage"),
        (Tag.Void, Tag.Cold, Proc.Rift, "Rift", "Void hits echo at 30% damage"),
        (Tag.Void, Tag.Lightning, Proc.Rift, "Rift", "Void hits echo at 30% damage"),
    };
}
