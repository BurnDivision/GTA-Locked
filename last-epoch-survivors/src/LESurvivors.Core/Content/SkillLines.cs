namespace LESurvivors.Core.Content;

/// <summary>One kind of improvement a skill rank-up can carry. Values are the Normal-rarity amount.</summary>
public enum SkillLine { Damage, Area, Frequency, Crit, Ailment, Multicast }

public static class SkillLines
{
    public static readonly SkillLine[] All = { SkillLine.Damage, SkillLine.Area, SkillLine.Frequency, SkillLine.Crit, SkillLine.Ailment, SkillLine.Multicast };

    public static double Weight(SkillLine l) => l switch
    {
        SkillLine.Damage => 30, SkillLine.Area => 16, SkillLine.Frequency => 22,
        SkillLine.Crit => 12, SkillLine.Ailment => 12, SkillLine.Multicast => 8, _ => 0,
    };

    /// <summary>Multicast is only offered at Rare+ and at most 3 times per skill.</summary>
    public static Rarity MinimumRarity(SkillLine l) => l == SkillLine.Multicast ? Rarity.Rare : Rarity.Normal;

    public static Stat AilmentStatFor(Tag primary) => primary switch
    {
        Tag.Fire => Stat.IgniteChance,
        Tag.Cold => Stat.ChillChance,
        Tag.Lightning => Stat.ShockChance,
        Tag.Poison => Stat.PoisonChance,
        Tag.Necrotic => Stat.PoisonChance,
        Tag.Void => Stat.BleedChance,
        _ => Stat.BleedChance,
    };

    /// <summary>The stat granted by a line at a rarity, for a skill whose primary damage tag is <paramref name="primary"/>.</summary>
    public static StatMod Mod(SkillLine l, Rarity r, Tag primary)
    {
        float m = r.Mag();
        return l switch
        {
            SkillLine.Damage => new StatMod(Stat.IncreasedDamage, 12f * m),
            SkillLine.Area => new StatMod(Stat.IncreasedArea, 10f * m),
            SkillLine.Frequency => new StatMod(Stat.CastFrequency, 10f * m),
            SkillLine.Crit => new StatMod(Stat.CritChance, 4f * m),
            SkillLine.Ailment => new StatMod(AilmentStatFor(primary), 15f * m),
            SkillLine.Multicast => new StatMod(Stat.Multicast, r >= Rarity.Legendary ? 2 : 1),
            _ => throw new ArgumentOutOfRangeException(nameof(l)),
        };
    }
}
