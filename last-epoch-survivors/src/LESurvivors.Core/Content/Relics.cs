namespace LESurvivors.Core.Content;

public enum RelicSlot { Amulet, Ring, Talisman, Idol }

/// <summary>Legendary signature powers (implemented by the adapter / run systems).</summary>
public enum RelicPower
{
    None,
    Chronoshard,     // every 10th cast of each skill repeats
    EmberHeart,      // +1% more fire damage per ignited enemy nearby (cap 30)
    WardensCoil,     // 15% less damage taken; +1 revive
    StormcallerEye,  // shocks chain to 1 extra enemy (sim: +10% more lightning damage)
    GravebloomSeed,  // kills heal 0.5% max health
    HourglassOfAges, // +25% cast frequency for 6s after each level-up
    GildedCompass,   // +60% pickup radius, +20 luck
}

public sealed record RelicAffix(string Id, StatMod[] Tiers); // Tiers[0..6] = T1..T7 values
public sealed record Relic(RelicSlot Slot, Rarity Rarity, StatMod[] Mods, RelicPower Power, string Name)
{
    public string Describe() => string.Join("\n", Mods.Select(m => m.ToString())) +
                                (Power != RelicPower.None ? "\n★ " + RelicCatalog.PowerText(Power) : "");
}

public static class RelicCatalog
{
    private static RelicAffix A(string id, Stat s, float t1, float step, Tag scope = Tag.None)
        => new(id, Enumerable.Range(0, 7).Select(i => new StatMod(s, t1 + step * i, scope)).ToArray());

    public static readonly Dictionary<RelicSlot, RelicAffix[]> AffixPool = new()
    {
        [RelicSlot.Amulet] = new[]
        {
            A("dmg", Stat.IncreasedDamage, 8, 6), A("crit", Stat.CritChance, 1.5f, 1), A("critm", Stat.CritMulti, 8, 6),
            A("freq", Stat.CastFrequency, 4, 2.5f), A("area", Stat.IncreasedArea, 5, 3), A("xp", Stat.XpGain, 4, 3),
        },
        [RelicSlot.Ring] = new[]
        {
            A("fire", Stat.IncreasedDamage, 10, 7, Tag.Fire), A("cold", Stat.IncreasedDamage, 10, 7, Tag.Cold),
            A("light", Stat.IncreasedDamage, 10, 7, Tag.Lightning), A("phys", Stat.IncreasedDamage, 10, 7, Tag.Physical),
            A("void", Stat.IncreasedDamage, 10, 7, Tag.Void), A("necro", Stat.IncreasedDamage, 10, 7, Tag.Necrotic),
            A("pois", Stat.IncreasedDamage, 10, 7, Tag.Poison), A("ail", Stat.AilmentEffect, 6, 4),
        },
        [RelicSlot.Talisman] = new[]
        {
            A("hp", Stat.MaxHealthPct, 6, 4), A("flathp", Stat.AddedHealth, 20, 15), A("regen", Stat.HealthRegen, 2, 2),
            A("armour", Stat.Armour, 60, 45), A("res", Stat.AllResistance, 5, 3), A("dr", Stat.DamageReduction, 2, 1.2f),
        },
        [RelicSlot.Idol] = new[]
        {
            A("move", Stat.MoveSpeed, 4, 2), A("pick", Stat.PickupRadius, 12, 8), A("luck", Stat.Luck, 6, 4),
            A("shard", Stat.ShardGain, 6, 5), A("heal", Stat.HealingEffect, 8, 6),
        },
    };

    public static readonly (RelicPower Power, RelicSlot Slot, string Name)[] Legendaries =
    {
        (RelicPower.Chronoshard, RelicSlot.Amulet, "Chronoshard"),
        (RelicPower.HourglassOfAges, RelicSlot.Amulet, "Hourglass of Ages"),
        (RelicPower.EmberHeart, RelicSlot.Ring, "Ember Heart"),
        (RelicPower.StormcallerEye, RelicSlot.Ring, "Stormcaller's Eye"),
        (RelicPower.WardensCoil, RelicSlot.Talisman, "Warden's Coil"),
        (RelicPower.GravebloomSeed, RelicSlot.Talisman, "Gravebloom Seed"),
        (RelicPower.GildedCompass, RelicSlot.Idol, "Gilded Compass"),
    };

    public static string PowerText(RelicPower p) => p switch
    {
        RelicPower.Chronoshard => "Every 10th cast of each skill happens twice",
        RelicPower.EmberHeart => "1% more Fire damage per ignited enemy nearby (max 30%)",
        RelicPower.WardensCoil => "15% less damage taken, +1 Revive",
        RelicPower.StormcallerEye => "Shocks spread to one extra enemy",
        RelicPower.GravebloomSeed => "Kills heal 0.5% of maximum health",
        RelicPower.HourglassOfAges => "+25% Cast Frequency for 6s after levelling up",
        RelicPower.GildedCompass => "+60% Pickup Radius, +20 Luck",
        _ => "",
    };

    public static StatMod[] PowerStats(RelicPower p) => p switch
    {
        RelicPower.WardensCoil => new[] { new StatMod(Stat.DamageReduction, 15), new StatMod(Stat.Revives, 1) },
        RelicPower.GildedCompass => new[] { new StatMod(Stat.PickupRadius, 60), new StatMod(Stat.Luck, 20) },
        RelicPower.StormcallerEye => new[] { new StatMod(Stat.MoreDamage, 10, Tag.Lightning) },
        _ => Array.Empty<StatMod>(),
    };

    private static readonly string[] Prefix = { "Worn", "Etched", "Gleaming", "Exalted" };

    public static Relic Roll(Rng rng, RelicSlot slot, Rarity rarity)
    {
        if (rarity == Rarity.Legendary)
        {
            var opts = Legendaries.Where(l => l.Slot == slot).ToArray();
            var leg = opts[rng.Range(0, opts.Length)];
            var baseMods = RollAffixes(rng, slot, 2, minTier: 4);
            return new Relic(slot, rarity, baseMods.Concat(PowerStats(leg.Power)).ToArray(), leg.Power, leg.Name);
        }
        int count = (int)rarity + 1;
        var mods = RollAffixes(rng, slot, count, minTier: (int)rarity, exaltedBoost: rarity == Rarity.Exalted);
        return new Relic(slot, rarity, mods, RelicPower.None, $"{Prefix[(int)rarity]} {slot}");
    }

    private static StatMod[] RollAffixes(Rng rng, RelicSlot slot, int count, int minTier, bool exaltedBoost = false)
    {
        var pool = AffixPool[slot].ToList();
        var result = new List<StatMod>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var a = pool[rng.Range(0, pool.Count)]; pool.Remove(a);
            int tier = Math.Min(6, minTier + rng.Range(0, 3));
            if (exaltedBoost && i == 0) tier = rng.Chance(0.3) ? 6 : 5;
            result.Add(a.Tiers[tier]);
        }
        return result.ToArray();
    }
}
