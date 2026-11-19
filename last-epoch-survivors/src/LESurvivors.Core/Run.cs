using LESurvivors.Core.Content;

namespace LESurvivors.Core;

public enum RunState { Running, Choosing, Paused, Victory, Defeat, Abandoned }

public sealed record RunConfig(CharacterClass Class, int Tier, IReadOnlyList<Omen> Omens, ulong Seed, int EnemyCap = 220);

public enum DropKind { XpMote, HealthOrb, Lodestone, TimeFracture, Shards, Cache }
public readonly record struct Drop(DropKind Kind, float Value, Rarity Rarity = Rarity.Normal);

/// <summary>What the player is choosing between right now.</summary>
public sealed class PendingChoice
{
    public bool IsCache { get; init; }
    public List<Card> Cards { get; init; } = new();
    public Relic? Relic { get; set; }   // caches may also offer a relic
    public int Picks { get; set; } = 1;  // caches give several upgrades in sequence
}

public sealed class RunResults
{
    public RunState Outcome; public float Time; public int Level; public long Kills; public long Shards;
    public int Tier; public int Corruption; public string Build = ""; public List<(string Skill, double Damage, int Casts)> SkillDamage = new();
    public Dictionary<string, long> ShardBreakdown = new();
}

/// <summary>
/// One Survivors run: owns time, XP, level, build, director and choice flow. Engine-agnostic;
/// the adapter feeds it kills/pickups/deaths and executes the requests it returns.
/// </summary>
public sealed class Run
{
    public RunConfig Config { get; }
    public Build Build { get; } = new();
    public Director Director { get; }
    public OfferGenerator Offers { get; }
    public Rng Rng { get; }
    public RunState State { get; private set; } = RunState.Running;
    public float Time { get; private set; }
    public int Level { get; private set; } = 1;
    public float Xp { get; private set; }
    public float XpNeeded => Leveling.XpToNext(Level);
    public long Kills { get; private set; }
    public long ShardsEarned { get; private set; }
    public Dictionary<string, long> ShardBreakdown { get; } = new();
    public int Corruption { get; }
    public int Rerolls, Banishes, Skips, RevivesLeft;
    public PendingChoice? Choice { get; private set; }
    private int _queuedLevelUps;
    private readonly Queue<PendingChoice> _queuedCaches = new();
    public bool OvertimeChosen { get; private set; }
    public float HourglassUntil { get; private set; } = -1;

    public event Action<SpawnRequest>? SpawnRequested;
    public event Action? StatsChanged;
    public event Action<PendingChoice>? ChoiceOpened;
    public event Action<RunState>? Ended;
    public event Action<float>? HealRequested; // fraction of max health

    public Run(RunConfig cfg, IReadOnlyList<SkillDef> pool, Profile profile)
    {
        Config = cfg;
        Rng = new Rng(cfg.Seed);
        Corruption = OmenCatalog.Corruption(cfg.Omens);
        Director = new Director(Rng, cfg.Tier, cfg.Omens, cfg.EnemyCap);
        var classSkills = pool.Where(p => p.Class == cfg.Class).ToList();
        Offers = new OfferGenerator(Build, classSkills.Count > 0 ? classSkills : pool, Rng);
        Build.ActiveSlots = profile.ActiveSlots;
        Build.RelicSlots = profile.RelicSlots;
        Build.Base.Add(profile.MetaStats());
        if (cfg.Omens.Contains(Omen.Frailty)) Build.Base.Add(new StatMod(Stat.MaxHealthPct, -20));
        if (cfg.Omens.Contains(Omen.Famine)) Build.Base.Add(new StatMod(Stat.HealingEffect, -30));

        var classPool = pool.Where(p => p.Class == cfg.Class).ToList();
        var start = SkillCatalog.StartingSkill[cfg.Class].Select(id => classPool.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault(p => p != null) ?? classPool.FirstOrDefault() ?? pool.First();
        Build.AddSkill(start);
        if (profile.Rank("prism") > 0) Build.AddOrRankGlyph(GlyphCatalog.All[Rng.Range(0, GlyphCatalog.All.Length)], Rarity.Magic);
        Build.Recompute();
        Rerolls = (int)Build.Global.Get(Stat.Rerolls);
        Banishes = (int)Build.Global.Get(Stat.Banishes);
        Skips = (int)Build.Global.Get(Stat.Skips);
        RevivesLeft = (int)Build.Global.Get(Stat.Revives);
    }

    public int ChoiceCount => 3 + (int)Build.Global.Get(Stat.ExtraChoices);
    public float Luck => Build.Global.Get(Stat.Luck) + (Config.Tier - 1) * 2 + Corruption * 0.1f;
    public float RewardMultiplier => (1f + 0.15f * (Config.Tier - 1)) * (1f + Corruption / 100f) * (1f + Build.Global.Get(Stat.ShardGain) / 100f);
    public bool IsActive => State is RunState.Running or RunState.Choosing or RunState.Paused;

    public void Tick(float dt, int liveEnemies)
    {
        if (State != RunState.Running) return;
        Time += dt;
        foreach (var r in Director.Update(Time, dt, liveEnemies)) SpawnRequested?.Invoke(r);
    }

    public void SetPaused(bool paused)
    {
        if (paused && State == RunState.Running) State = RunState.Paused;
        else if (!paused && State == RunState.Paused) State = RunState.Running;
    }

    /// <summary>Called by the adapter for every run enemy that dies. Returns drops to spawn at the corpse.</summary>
    public List<Drop> OnEnemyKilled(EnemyKind kind)
    {
        var drops = new List<Drop>();
        if (!IsActive) return drops;
        Kills++;
        AddShards("Kills", kind switch { EnemyKind.Elite => 3, EnemyKind.Warden => 40, EnemyKind.Harbinger => 250, _ => Rng.Chance(0.06) ? 1 : 0 });
        float xp = kind switch { EnemyKind.Elite => 25, EnemyKind.Warden => 150, EnemyKind.Harbinger => 500, _ => Rng.Chance(0.08) ? 5 : 1 };
        drops.Add(new Drop(DropKind.XpMote, xp));
        double orb = (Config.Omens.Contains(Omen.Famine) ? 0.005 : 0.01);
        if (kind == EnemyKind.Normal && Rng.Chance(orb)) drops.Add(new Drop(DropKind.HealthOrb, 0.2f));
        if (kind == EnemyKind.Normal && Rng.Chance(0.0015)) drops.Add(new Drop(DropKind.Lodestone, 0));
        if (kind == EnemyKind.Normal && Rng.Chance(0.001)) drops.Add(new Drop(DropKind.TimeFracture, 4));
        if (kind == EnemyKind.Elite) drops.Add(new Drop(DropKind.Cache, 1, Rarities.Roll(Rng, Luck)));
        if (kind == EnemyKind.Warden) drops.Add(new Drop(DropKind.Cache, 3, Rarities.Roll(Rng, Luck + 40, Rarity.Exalted)));
        if (kind == EnemyKind.Harbinger)
        {
            Director.MarkHarbingerDefeated();
            AddShards("Victory", (long)(400 * Config.Tier));
            State = RunState.Victory;
            Ended?.Invoke(State);
        }
        return drops;
    }

    private void AddShards(string source, long baseAmount)
    {
        if (baseAmount <= 0) return;
        long v = (long)Math.Round(baseAmount * RewardMultiplier);
        ShardsEarned += v; ShardBreakdown[source] = ShardBreakdown.GetValueOrDefault(source) + v;
    }

    /// <summary>Player picked up a drop.</summary>
    public void Collect(Drop d)
    {
        if (!IsActive) return;
        switch (d.Kind)
        {
            case DropKind.XpMote: GainXp(d.Value); break;
            case DropKind.HealthOrb: HealRequested?.Invoke(d.Value * (1f + Build.Global.Get(Stat.HealingEffect) / 100f)); break;
            case DropKind.Shards: AddShards("Pickups", (long)d.Value); break;
            case DropKind.Cache: OpenCache(d.Rarity, (int)d.Value); break;
        }
    }

    public void GainXp(float amount)
    {
        Xp += amount * (1f + Build.Global.Get(Stat.XpGain) / 100f);
        while (Xp >= XpNeeded)
        {
            Xp -= XpNeeded; Level++; _queuedLevelUps++;
            Build.Base.Add(Leveling.PerLevel());
        }
        if (_queuedLevelUps > 0) { Build.Recompute(); StatsChanged?.Invoke(); TryOpenNext(); }
    }

    private void OpenCache(Rarity r, int basePicks)
    {
        int picks = basePicks + (int)r / 2;
        Relic? relic = null;
        if (Rng.Chance(0.35 + 0.15 * (int)r))
        {
            var slots = Enum.GetValues<RelicSlot>().Take(Build.RelicSlots).ToArray();
            relic = RelicCatalog.Roll(Rng, slots[Rng.Range(0, slots.Length)], r);
        }
        _queuedCaches.Enqueue(new PendingChoice { IsCache = true, Picks = Math.Max(1, picks), Relic = relic });
        TryOpenNext();
    }

    private void TryOpenNext()
    {
        if (Choice != null || !IsActive || State == RunState.Paused) return;
        if (_queuedCaches.Count > 0)
        {
            var c = _queuedCaches.Dequeue();
            c.Cards.AddRange(Offers.Generate(ChoiceCount, Luck + 25, Rarity.Magic));
            Open(c);
        }
        else if (_queuedLevelUps > 0)
        {
            _queuedLevelUps--;
            Open(new PendingChoice { Cards = Offers.Generate(ChoiceCount, Luck) });
        }
    }

    private void Open(PendingChoice c) { Choice = c; State = RunState.Choosing; ChoiceOpened?.Invoke(c); }

    private void CloseChoice()
    {
        Choice = null;
        if (State == RunState.Choosing) State = RunState.Running;
        TryOpenNext();
    }

    public void Choose(int index)
    {
        var c = Choice ?? throw new InvalidOperationException("No choice open");
        var card = c.Cards[index];
        if (!Offers.Apply(card))
        {
            if (card.Kind == CardKind.Heal) HealRequested?.Invoke(0.3f);
            if (card.Kind == CardKind.Shards) AddShards("Cards", 25);
        }
        if (Build.HasPower(RelicPower.HourglassOfAges) && !c.IsCache) HourglassUntil = Time + 6f;
        StatsChanged?.Invoke();
        c.Picks--;
        if (c.Picks > 0) { c.Cards.Clear(); c.Cards.AddRange(Offers.Generate(ChoiceCount, Luck + 25, Rarity.Magic)); ChoiceOpened?.Invoke(c); return; }
        CloseChoice();
    }

    /// <summary>Equip (true) or salvage (false) the relic offered by the open cache.</summary>
    public Relic? ResolveRelic(bool equip)
    {
        var c = Choice; if (c?.Relic == null) return null;
        Relic? replaced = null;
        if (equip) { replaced = Build.EquipRelic(c.Relic); StatsChanged?.Invoke(); }
        else AddShards("Salvage", 10 + 10 * (int)c.Relic.Rarity);
        c.Relic = null;
        return replaced;
    }

    public bool Reroll()
    {
        if (Choice == null || Rerolls <= 0) return false;
        Rerolls--;
        var cards = Offers.Generate(ChoiceCount, Luck + (Choice.IsCache ? 25 : 0), Choice.IsCache ? Rarity.Magic : Rarity.Normal);
        Choice.Cards.Clear(); Choice.Cards.AddRange(cards); ChoiceOpened?.Invoke(Choice);
        return true;
    }

    public bool Banish(int index)
    {
        if (Choice == null || Banishes <= 0) return false;
        var card = Choice.Cards[index];
        if (card.Kind is CardKind.Heal or CardKind.Shards or CardKind.Ascend) return false;
        Banishes--; Offers.Banished.Add(card.SubjectId);
        Choice.Cards.RemoveAt(index);
        var extra = Offers.Generate(ChoiceCount, Luck).FirstOrDefault(n => Choice.Cards.All(x => x.SubjectId != n.SubjectId));
        if (extra != null) Choice.Cards.Insert(index, extra);
        ChoiceOpened?.Invoke(Choice);
        return true;
    }

    public bool Skip()
    {
        if (Choice == null || Choice.IsCache || Skips <= 0) return false;
        Skips--; Xp += XpNeeded * 0.25f; CloseChoice();
        return true;
    }

    /// <summary>The player died. Returns true if a revive was consumed.</summary>
    public bool OnPlayerDied()
    {
        if (!IsActive) return false;
        if (RevivesLeft > 0) { RevivesLeft--; HealRequested?.Invoke(1f); return true; }
        State = RunState.Defeat; Ended?.Invoke(State); return false;
    }

    /// <summary>After victory the player may continue into endless Overtime.</summary>
    public void ContinueIntoOvertime()
    {
        if (State != RunState.Victory) return;
        OvertimeChosen = true; State = RunState.Running;
    }

    public void Abandon() { if (IsActive) { State = RunState.Abandoned; Ended?.Invoke(State); } }

    /// <summary>Ends overtime (or confirms the end screen) and returns the final results.</summary>
    public RunResults Results()
    {
        if (OvertimeChosen && State == RunState.Running) { State = RunState.Victory; }
        if (OvertimeChosen) AddShards("Overtime", (long)(Math.Max(0, Time - Director.RunLength) / 60f * 30));
        AddShards("Survival", (long)(Time / 60f * 6));
        return new RunResults
        {
            Outcome = State, Time = Time, Level = Level, Kills = Kills, Shards = ShardsEarned, Tier = Config.Tier,
            Corruption = Corruption, Build = Build.Summary(), ShardBreakdown = new(ShardBreakdown),
            SkillDamage = Build.Skills.Select(s => (s.Def.Name, s.DamageDealt, s.Casts)).ToList(),
        };
    }

    /// <summary>Commit results to the profile (call exactly once).</summary>
    public static void Commit(Profile p, RunConfig cfg, RunResults r)
    {
        p.Shards += r.Shards; p.LifetimeShards += r.Shards; p.TotalKills += r.Kills;
        if (r.Outcome == RunState.Victory) { p.RunsWon++; p.HighestTierCleared = Math.Max(p.HighestTierCleared, cfg.Tier); }
        var key = cfg.Class.ToString();
        if (!p.Records.TryGetValue(key, out var rec)) p.Records[key] = rec = new RecordEntry();
        rec.BestTime = Math.Max(rec.BestTime, r.Time); rec.BestKills = Math.Max(rec.BestKills, r.Kills);
        rec.BestLevel = Math.Max(rec.BestLevel, r.Level);
        if (r.Outcome == RunState.Victory) rec.BestTier = Math.Max(rec.BestTier, cfg.Tier);
    }
}
