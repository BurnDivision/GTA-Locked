namespace LESurvivors.Core;

/// <summary>Mirrors Last Epoch's <c>AT</c> ability-tag flags (same numeric values) so the adapter can cast directly.</summary>
[Flags]
public enum Tag : int
{
    None = 0,
    Physical = 1, Lightning = 2, Cold = 4, Fire = 8, Void = 16, Necrotic = 32, Poison = 64,
    Elemental = 128, Spell = 256, Melee = 512, Throwing = 1024, Bow = 2048, DoT = 4096,
    Minion = 8192, Totem = 16384, Buff = 131072, Channelling = 262144, Transform = 524288,
    Curse = 16777216, Ailment = 33554432,
}

public static class Tags
{
    /// <summary>Tags that count toward Resonance.</summary>
    public static readonly Tag[] Resonant =
    {
        Tag.Physical, Tag.Fire, Tag.Cold, Tag.Lightning, Tag.Void, Tag.Necrotic, Tag.Poison,
        Tag.Spell, Tag.Melee, Tag.Throwing, Tag.Bow, Tag.DoT, Tag.Minion,
    };

    public static readonly Tag[] DamageTypes =
        { Tag.Physical, Tag.Fire, Tag.Cold, Tag.Lightning, Tag.Void, Tag.Necrotic, Tag.Poison };

    public static IEnumerable<Tag> Split(Tag t)
    {
        foreach (var r in Resonant) if ((t & r) != 0) yield return r;
    }

    /// <summary>First damage-type tag present (used for ailment and Ascension pairing).</summary>
    public static Tag PrimaryDamage(Tag t)
    {
        foreach (var d in DamageTypes) if ((t & d) != 0) return d;
        return Tag.Physical;
    }

    public static string Name(Tag t) => t.ToString();
}
