namespace LESurvivors.Core.Content;

public enum Targeting { Nearest, Densest, RandomInRange, Self, MoveDirection }

public enum CharacterClass { Primalist = 0, Mage = 1, Sentinel = 2, Acolyte = 3, Rogue = 4 }

/// <summary>An auto-cast weapon. Built at runtime from a real LE Ability plus the curated overrides below.</summary>
public sealed record SkillDef(
    string Id,              // LE ability name (stable key, e.g. "Fireball")
    string Name,
    CharacterClass Class,
    Tag Tags,
    float BaseInterval,     // seconds between casts at rank 1, before Cast Frequency
    Targeting Targeting,
    float Range,            // metres; target search radius
    float BaseDamage = 10f) // used only by the pacing simulator
{
    public Tag PrimaryDamage => LESurvivors.Core.Tags.PrimaryDamage(Tags);
}

/// <summary>
/// Curated knowledge about LE skills as auto-weapons. Keys are LE <c>Ability.abilityName</c> values.
/// Anything not listed is inferred from its tags. Skills listed in <see cref="Excluded"/> never enter the pool.
/// </summary>
public static class SkillCatalog
{
    public sealed record Override(float Interval, Targeting Targeting, float Range, float SimDamage = 10f);

    public static readonly Dictionary<string, Override> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mage
        ["Fireball"] = new(0.9f, Targeting.Nearest, 16, 12),
        ["Lightning Blast"] = new(0.8f, Targeting.Nearest, 14, 11),
        ["Glacier"] = new(1.6f, Targeting.Densest, 12, 22),
        ["Elemental Nova"] = new(1.8f, Targeting.Self, 6, 24),
        ["Frost Claw"] = new(1.4f, Targeting.Nearest, 12, 18),
        ["Volcanic Orb"] = new(2.2f, Targeting.Nearest, 16, 30),
        ["Meteor"] = new(3.0f, Targeting.Densest, 18, 60),
        ["Static Orb"] = new(1.6f, Targeting.Nearest, 16, 20),
        ["Snap Freeze"] = new(4.0f, Targeting.Self, 7, 30),
        ["Ice Barrage"] = new(3.0f, Targeting.Densest, 14, 40),
        ["Shatter Strike"] = new(1.0f, Targeting.Nearest, 3, 14),
        ["Mana Strike"] = new(0.8f, Targeting.Nearest, 3, 12),
        ["Black Hole"] = new(6.0f, Targeting.Densest, 14, 70),
        ["Arcane Ascendance"] = new(8.0f, Targeting.Self, 6, 60),
        ["Runic Invocation"] = new(2.5f, Targeting.Densest, 14, 35),
        // Sentinel
        ["Hammer Throw"] = new(1.1f, Targeting.Nearest, 14, 16),
        ["Smite"] = new(1.0f, Targeting.Nearest, 12, 16),
        ["Judgement"] = new(1.6f, Targeting.Nearest, 4, 30),
        ["Rive"] = new(0.6f, Targeting.Nearest, 3, 10),
        ["Vengeance"] = new(0.7f, Targeting.Nearest, 3, 12),
        ["Javelin"] = new(1.0f, Targeting.Nearest, 16, 18),
        ["Shield Throw"] = new(1.4f, Targeting.Nearest, 14, 20),
        ["Erasing Strike"] = new(2.0f, Targeting.Nearest, 4, 40),
        ["Forge Strike"] = new(1.2f, Targeting.Nearest, 3, 18),
        ["Sigils of Hope"] = new(6.0f, Targeting.Self, 0, 0),
        ["Holy Aura"] = new(10.0f, Targeting.Self, 0, 0),
        // Primalist
        ["Tornado"] = new(1.8f, Targeting.Nearest, 12, 24),
        ["Gathering Storm"] = new(0.8f, Targeting.Nearest, 4, 14),
        ["Avalanche"] = new(2.4f, Targeting.Densest, 14, 45),
        ["Earthquake"] = new(2.0f, Targeting.MoveDirection, 8, 32),
        ["Upheaval"] = new(1.3f, Targeting.Nearest, 8, 22),
        ["Warcry"] = new(6.0f, Targeting.Self, 6, 25),
        ["Entangling Roots"] = new(2.5f, Targeting.Densest, 12, 25),
        ["Serpent Strike"] = new(0.9f, Targeting.Nearest, 3, 14),
        ["Maelstrom"] = new(3.0f, Targeting.Self, 5, 30),
        ["Thorn Totem"] = new(4.0f, Targeting.Densest, 10, 30),
        ["Swipe"] = new(0.7f, Targeting.Nearest, 3, 11),
        // Acolyte
        ["Hungering Souls"] = new(0.9f, Targeting.Nearest, 16, 13),
        ["Rip Blood"] = new(0.8f, Targeting.Nearest, 12, 12),
        ["Bone Curse"] = new(4.0f, Targeting.Densest, 12, 20),
        ["Marrow Shards"] = new(1.0f, Targeting.Nearest, 14, 15),
        ["Spirit Plague"] = new(1.2f, Targeting.Nearest, 14, 16),
        ["Death Seal"] = new(5.0f, Targeting.Self, 6, 45),
        ["Chaos Bolts"] = new(1.2f, Targeting.Nearest, 16, 20),
        ["Harvest"] = new(0.8f, Targeting.Nearest, 4, 14),
        ["Aura of Decay"] = new(8.0f, Targeting.Self, 5, 25),
        ["Soul Feast"] = new(5.0f, Targeting.Densest, 12, 30),
        ["Drain Life"] = new(1.2f, Targeting.Nearest, 12, 14),
        ["Wandering Spirits"] = new(4.0f, Targeting.Self, 10, 30),
        // Rogue
        ["Shurikens"] = new(1.0f, Targeting.Self, 6, 16),
        ["Umbral Blades"] = new(1.2f, Targeting.Nearest, 14, 20),
        ["Multishot"] = new(1.0f, Targeting.Nearest, 16, 18),
        ["Hail of Arrows"] = new(2.2f, Targeting.Densest, 16, 35),
        ["Puncture"] = new(0.6f, Targeting.Nearest, 3, 10),
        ["Cinder Strike"] = new(1.4f, Targeting.Nearest, 10, 24),
        ["Detonating Arrow"] = new(1.4f, Targeting.Nearest, 16, 26),
        ["Explosive Trap"] = new(1.8f, Targeting.Densest, 10, 28),
        ["Acid Flask"] = new(1.4f, Targeting.Densest, 12, 18),
        ["Dark Quiver"] = new(6.0f, Targeting.Self, 0, 0),
        ["Shadow Cascade"] = new(1.4f, Targeting.Nearest, 6, 24),
        ["Blade Flurry"] = new(0.8f, Targeting.Nearest, 3, 12),
        ["Lethal Mirage"] = new(5.0f, Targeting.Nearest, 10, 50),
        ["Ballista"] = new(4.0f, Targeting.Densest, 10, 30),
    };

    /// <summary>Movement, transform, channelled, pure-utility and minion-command skills are not auto-weapons in v1.</summary>
    public static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "Teleport", "Flame Rush", "Shield Rush", "Lunge", "Volatile Reversal", "Fury Leap", "Transplant", "Shift",
        "Smoke Bomb", "Decoy", "Evade", "Disintegrate", "Warpath", "Flurry", "Focus", "Flame Ward", "Frost Wall",
        "Manifest Armor", "Summon Wolf", "Summon Bear", "Summon Raptor", "Summon Scorpion", "Summon Spriggan",
        "Summon Frenzy Totem", "Summon Storm Totem", "Summon Thorn Totem", "Summon Skeleton", "Summon Bone Golem",
        "Summon Volatile Zombie", "Summon Wraith", "Summon Skeletal Mage", "Dread Shade", "Assemble Abomination",
        "Werebear Form", "Spriggan Form", "Swarmblade Form", "Reap", "Profane Veil", "Infernal Shade",
        "Healing Hands", "Rebuke", "Ring of Shields", "Sacrifice", "Transplant", "Falconry", "Aerial Assault",
        "Dive Bomb", "Synchronized Strike", "Heartseeker", "Enchant Weapon", "Static", "Surge", "Mark for Death",
        "Spellblade", "Ice Thorns",
    };

    public static bool IsAutoWeaponCandidate(string abilityName, Tag tags)
    {
        if (Excluded.Contains(abilityName)) return false;
        if ((tags & (Tag.Channelling | Tag.Transform)) != 0) return false;
        if ((tags & Tag.Minion) != 0 && !Overrides.ContainsKey(abilityName)) return false;
        return (tags & (Tag.Physical | Tag.Fire | Tag.Cold | Tag.Lightning | Tag.Void | Tag.Necrotic | Tag.Poison)) != 0
               || Overrides.ContainsKey(abilityName);
    }

    /// <summary>Builds a SkillDef for any LE ability, using an override when available.</summary>
    public static SkillDef Build(string abilityName, CharacterClass cls, Tag tags, float leCooldown)
    {
        if (Overrides.TryGetValue(abilityName, out var o))
            return new SkillDef(abilityName, abilityName, cls, tags, o.Interval, o.Targeting, o.Range, o.SimDamage);
        bool melee = (tags & Tag.Melee) != 0;
        float interval = Math.Max(melee ? 0.7f : 1.0f, leCooldown > 0 ? leCooldown * 0.6f : 0f);
        if ((tags & Tag.Buff) != 0) return new SkillDef(abilityName, abilityName, cls, tags, Math.Max(6f, interval), Targeting.Self, 0, 0);
        return new SkillDef(abilityName, abilityName, cls, tags, interval, Targeting.Nearest, melee ? 3f : 14f);
    }

    /// <summary>The class's starting weapon, used when available in the runtime pool.</summary>
    public static readonly Dictionary<CharacterClass, string[]> StartingSkill = new()
    {
        [CharacterClass.Mage] = new[] { "Fireball", "Lightning Blast", "Frost Claw" },
        [CharacterClass.Sentinel] = new[] { "Hammer Throw", "Smite", "Javelin" },
        [CharacterClass.Primalist] = new[] { "Tornado", "Upheaval", "Gathering Storm" },
        [CharacterClass.Acolyte] = new[] { "Hungering Souls", "Rip Blood", "Marrow Shards" },
        [CharacterClass.Rogue] = new[] { "Shurikens", "Umbral Blades", "Multishot" },
    };
}
