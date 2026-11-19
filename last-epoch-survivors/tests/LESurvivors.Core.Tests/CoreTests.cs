using LESurvivors.Core;
using LESurvivors.Core.Content;
using LESurvivors.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace LESurvivors.Core.Tests;

public class RarityTests
{
    [Fact]
    public void LuckShiftsWeightUpwardsAndRespectsMinimum()
    {
        var w0 = Rarities.Weights(0); var w100 = Rarities.Weights(100);
        Assert.True(w100[0] < w0[0]);
        Assert.True(w100[4] > w0[4]);
        Assert.Equal(w0.Sum(), w100.Sum(), 6);
        var min = Rarities.Weights(0, Rarity.Rare);
        Assert.Equal(0, min[0]); Assert.Equal(0, min[1]);
    }

    [Fact]
    public void RollDistributionRoughlyMatchesWeights()
    {
        var rng = new Rng(42); var counts = new int[5];
        for (int i = 0; i < 100_000; i++) counts[(int)Rarities.Roll(rng, 0)]++;
        Assert.InRange(counts[0] / 1000.0, 57, 63);
        Assert.InRange(counts[4] / 1000.0, 0.3, 0.8);
    }
}

public class BuildTests
{
    private static SkillDef Fireball => SkillCatalog.Build("Fireball", CharacterClass.Mage, Tag.Fire | Tag.Spell, 0);
    private static SkillDef Meteor => SkillCatalog.Build("Meteor", CharacterClass.Mage, Tag.Fire | Tag.Spell, 0);

    [Fact]
    public void ResonanceActivatesAtThresholdsAndAddsStats()
    {
        var b = new Build();
        b.AddSkill(Fireball);
        Assert.False(b.ResonanceTier.ContainsKey(Tag.Fire));
        b.AddOrRankGlyph(GlyphCatalog.Get("embers"), Rarity.Normal);
        Assert.Equal(1, b.ResonanceTier[Tag.Fire]);
        Assert.Equal(20 + 10, b.Global.GetFor(Stat.IncreasedDamage, Tag.Fire), 3); // resonance 20 + glyph 10
        b.AddSkill(Meteor);
        b.AddOrRankGlyph(GlyphCatalog.Get("arcana"), Rarity.Normal); // spell, not fire
        Assert.Equal(1, b.ResonanceTier[Tag.Fire]); // fire count = 3
        b.AddSkill(SkillCatalog.Build("Volcanic Orb", CharacterClass.Mage, Tag.Fire | Tag.Spell, 0));
        Assert.Equal(2, b.ResonanceTier[Tag.Fire]);   // fire count = 4
        Assert.Equal(2, b.ResonanceTier[Tag.Spell]);  // spell count = 4
    }

    [Fact]
    public void ScopedStatsOnlyApplyToMatchingSkills()
    {
        var b = new Build();
        var fb = b.AddSkill(Fireball);
        var ham = b.AddSkill(SkillCatalog.Build("Hammer Throw", CharacterClass.Sentinel, Tag.Physical | Tag.Throwing, 0));
        b.AddOrRankGlyph(GlyphCatalog.Get("embers"), Rarity.Rare);
        Assert.True(b.DamageMultiplier(fb) > b.DamageMultiplier(ham));
        Assert.Equal(1f, b.DamageMultiplier(ham), 3);
    }

    [Fact]
    public void FrequencyShortensIntervalAndMulticastAddsCasts()
    {
        var b = new Build(); var s = b.AddSkill(Fireball);
        float before = b.IntervalOf(s);
        b.RankSkill("Fireball", SkillLine.Frequency, Rarity.Exalted);
        Assert.True(b.IntervalOf(s) < before);
        b.RankSkill("Fireball", SkillLine.Multicast, Rarity.Rare);
        Assert.Equal(2, b.CastsPerTrigger(s));
    }

    [Fact]
    public void AscensionNeedsRankTenAndResonanceTwo()
    {
        var b = new Build(); var s = b.AddSkill(Fireball);
        for (int i = 0; i < 9; i++) b.RankSkill("Fireball", SkillLine.Damage, Rarity.Normal);
        Assert.Equal(10, s.Rank);
        Assert.False(b.IsAscensionReady(s)); // fire count only 1
        b.AddSkill(Meteor);
        b.AddOrRankGlyph(GlyphCatalog.Get("embers"), Rarity.Normal);
        b.AddSkill(SkillCatalog.Build("Volcanic Orb", CharacterClass.Mage, Tag.Fire | Tag.Spell, 0));
        Assert.True(b.IsAscensionReady(s));
        var mult = b.DamageMultiplier(s);
        b.Ascend("Fireball");
        Assert.True(s.Ascended);
        Assert.Equal(2, b.CastsPerTrigger(s));
        Assert.True(b.DamageMultiplier(s) > mult * 1.7f);
        Assert.Throws<InvalidOperationException>(() => b.Ascend("Fireball"));
    }

    [Fact]
    public void ReactionsNeedTwoTierTwoResonances()
    {
        var b = new Build { ActiveSlots = 6 };
        foreach (var n in new[] { "Lightning Blast", "Static Orb" }) b.AddSkill(SkillCatalog.Build(n, CharacterClass.Mage, Tag.Lightning | Tag.Spell, 0));
        b.AddOrRankGlyph(GlyphCatalog.Get("storm"), Rarity.Normal);
        b.AddOrRankGlyph(GlyphCatalog.Get("embers"), Rarity.Normal);
        Assert.DoesNotContain("Overload", b.ActiveReactions);
        b.AddSkill(Fireball); b.AddSkill(Meteor);
        b.AddSkill(SkillCatalog.Build("Elemental Nova", CharacterClass.Mage, Tag.Fire | Tag.Lightning | Tag.Cold | Tag.Spell, 0));
        Assert.Contains("Overload", b.ActiveReactions);
        Assert.Contains(Proc.Overload, b.ActiveProcs);
    }

    [Fact]
    public void SlotLimitsAreEnforced()
    {
        var b = new Build { ActiveSlots = 1 };
        b.AddSkill(Fireball);
        Assert.Throws<InvalidOperationException>(() => b.AddSkill(Meteor));
        for (int i = 0; i < Build.MaxGlyphs; i++) b.AddOrRankGlyph(GlyphCatalog.All[i], Rarity.Normal);
        Assert.Throws<InvalidOperationException>(() => b.AddOrRankGlyph(GlyphCatalog.All[Build.MaxGlyphs], Rarity.Normal));
    }
}

public class OfferTests
{
    [Fact]
    public void OffersAreUniqueAndValid()
    {
        var pool = Simulator.CatalogPool();
        var b = new Build(); b.AddSkill(pool.First(p => p.Id == "Fireball"));
        var gen = new OfferGenerator(b, pool, new Rng(7));
        for (int i = 0; i < 500; i++)
        {
            var cards = gen.Generate(4, 20);
            Assert.Equal(4, cards.Count);
            Assert.Equal(cards.Count, cards.Select(c => c.SubjectId).Distinct().Count());
            Assert.All(cards.Where(c => c.Line == SkillLine.Multicast && c.Kind == CardKind.SkillRank), c => Assert.True(c.Rarity >= Rarity.Rare));
        }
    }

    [Fact]
    public void BanishedSubjectsNeverReturn()
    {
        var pool = Simulator.CatalogPool();
        var b = new Build(); b.AddSkill(pool.First(p => p.Id == "Fireball"));
        var gen = new OfferGenerator(b, pool, new Rng(9));
        gen.Banished.Add("embers"); gen.Banished.Add("Meteor");
        for (int i = 0; i < 300; i++)
            Assert.DoesNotContain(gen.Generate(3, 0), c => c.SubjectId is "embers" or "Meteor");
    }

    [Fact]
    public void CompleteBuildFallsBackToFillers()
    {
        var pool = Simulator.CatalogPool().Take(1).ToList();
        var b = new Build { ActiveSlots = 1 };
        var s = b.AddSkill(pool[0]);
        for (int i = 1; i < Build.MaxSkillRank; i++) b.RankSkill(s.Def.Id, SkillLine.Damage, Rarity.Normal);
        foreach (var g in GlyphCatalog.All.Take(Build.MaxGlyphs)) for (int r = 0; r < g.MaxRank; r++) b.AddOrRankGlyph(g, Rarity.Normal);
        var cards = new OfferGenerator(b, pool, new Rng(1)).Generate(3, 0);
        Assert.All(cards, c => Assert.True(c.Kind is CardKind.Heal or CardKind.Shards or CardKind.Ascend));
    }
}

public class DirectorTests
{
    [Fact]
    public void EventsHappenOnSchedule()
    {
        var d = new Director(new Rng(3), 1, Array.Empty<Omen>(), 220);
        var all = new List<(float t, SpawnRequest r)>();
        for (float t = 0; t < Director.RunLength + 2; t += 0.25f) foreach (var r in d.Update(t, 0.25f, 0)) all.Add((t, r));
        Assert.Equal(3, all.Count(x => x.r.Kind == EnemyKind.Warden));
        Assert.Single(all, x => x.r.Kind == EnemyKind.Harbinger);
        Assert.InRange(all.Count(x => x.r.Kind == EnemyKind.Elite), 18, 20);
        Assert.True(all.First(x => x.r.Kind == EnemyKind.Warden).t >= 300);
    }

    [Fact]
    public void TargetCountGrowsAndRespectsCap()
    {
        var d = new Director(new Rng(1), 1, Array.Empty<Omen>(), 150);
        Assert.True(d.TargetAlive(60) < d.TargetAlive(600));
        Assert.True(d.TargetAlive(1200) <= 150);
        var swarm = new Director(new Rng(1), 1, new[] { Omen.Swarm }, 150);
        Assert.True(swarm.TargetAlive(600) > d.TargetAlive(600));
    }

    [Fact]
    public void ScalingIncreasesWithTimeTierAndOmens()
    {
        var a = new Director(new Rng(1), 1, Array.Empty<Omen>(), 220);
        var b = new Director(new Rng(1), 5, new[] { Omen.Brutality, Omen.Tenacity }, 220);
        Assert.True(a.Scaling(600, EnemyKind.Normal).Health > a.Scaling(60, EnemyKind.Normal).Health);
        Assert.True(b.Scaling(600, EnemyKind.Normal).Health > a.Scaling(600, EnemyKind.Normal).Health);
        Assert.True(b.Scaling(600, EnemyKind.Normal).Damage > a.Scaling(600, EnemyKind.Normal).Damage * 1.25f);
        Assert.True(b.Scaling(600, EnemyKind.Warden).Health > a.Scaling(600, EnemyKind.Warden).Health * 1.4f);
        // tier pressure is not applied in the opening, only omens are
        Assert.Equal(a.Scaling(30, EnemyKind.Normal).Health, new Director(new Rng(1), 10, Array.Empty<Omen>(), 220).Scaling(30, EnemyKind.Normal).Health, 3);
    }

    [Fact]
    public void NoTrashWhileHarbingerAlive()
    {
        var d = new Director(new Rng(1), 1, Array.Empty<Omen>(), 220);
        d.Update(Director.RunLength, 0.1f, 0);
        Assert.True(d.HarbingerAlive);
        Assert.Empty(d.Update(Director.RunLength + 5, 5f, 0));
    }
}

public class RunTests
{
    private static Run NewRun(Profile? p = null, CharacterClass c = CharacterClass.Mage, int tier = 1, params Omen[] omens)
        => new(new RunConfig(c, tier, omens, 1234), Simulator.CatalogPool(), p ?? new Profile());

    [Fact]
    public void StartsWithClassStartingSkill()
    {
        Assert.Equal("Fireball", NewRun().Build.Skills.Single().Def.Id);
        Assert.Equal("Hammer Throw", NewRun(c: CharacterClass.Sentinel).Build.Skills.Single().Def.Id);
        Assert.Equal("Shurikens", NewRun(c: CharacterClass.Rogue).Build.Skills.Single().Def.Id);
    }

    [Fact]
    public void XpProducesLevelUpChoicesAndQueues()
    {
        var run = NewRun(); int opened = 0; run.ChoiceOpened += _ => opened++;
        run.GainXp(Leveling.XpToNext(1) + Leveling.XpToNext(2) + 0.1f);
        Assert.Equal(3, run.Level);
        Assert.Equal(RunState.Choosing, run.State);
        Assert.Equal(3, run.Choice!.Cards.Count);
        run.Choose(0);
        Assert.Equal(RunState.Choosing, run.State); // second queued level-up
        run.Choose(0);
        Assert.Equal(RunState.Running, run.State);
        Assert.Equal(2, opened);
    }

    [Fact]
    public void TimeDoesNotAdvanceWhileChoosingOrPaused()
    {
        var run = NewRun();
        run.Tick(1, 0); Assert.Equal(1, run.Time, 3);
        run.GainXp(100); run.Tick(5, 0); Assert.Equal(1, run.Time, 3);
        while (run.Choice != null) run.Choose(0);
        run.SetPaused(true); run.Tick(5, 0); Assert.Equal(1, run.Time, 3);
        run.SetPaused(false); run.Tick(1, 0); Assert.Equal(2, run.Time, 3);
    }

    [Fact]
    public void ReviveThenDefeat()
    {
        var p = new Profile(); p.Nodes["secondwind"] = 1;
        var run = NewRun(p);
        Assert.True(run.OnPlayerDied());
        Assert.True(run.IsActive);
        Assert.False(run.OnPlayerDied());
        Assert.Equal(RunState.Defeat, run.State);
    }

    [Fact]
    public void HarbingerKillWinsAndOvertimeContinues()
    {
        var run = NewRun();
        run.OnEnemyKilled(EnemyKind.Harbinger);
        Assert.Equal(RunState.Victory, run.State);
        run.ContinueIntoOvertime();
        Assert.Equal(RunState.Running, run.State);
        run.Tick(Director.RunLength + 60, 0);
        var res = run.Results();
        Assert.Equal(RunState.Victory, res.Outcome);
        Assert.True(res.ShardBreakdown.ContainsKey("Overtime"));
    }

    [Fact]
    public void CachesGiveMultiplePicksAndRelics()
    {
        var run = NewRun();
        run.Collect(new Drop(DropKind.Cache, 3, Rarity.Exalted));
        Assert.True(run.Choice!.IsCache);
        int picks = run.Choice.Picks;
        Assert.True(picks >= 3);
        for (int i = 0; i < picks; i++) { if (run.Choice?.Relic != null) run.ResolveRelic(true); run.Choose(0); }
        Assert.Null(run.Choice);
    }

    [Fact]
    public void RerollBanishSkipConsumeCharges()
    {
        var p = new Profile(); p.Nodes["reroll"] = 1; p.Nodes["banish"] = 1; p.Nodes["skip"] = 1;
        var run = NewRun(p);
        run.GainXp(10);
        Assert.True(run.Reroll()); Assert.False(run.Reroll());
        var idx = run.Choice!.Cards.FindIndex(c => c.Kind is not (CardKind.Heal or CardKind.Shards or CardKind.Ascend));
        Assert.True(run.Banish(idx)); Assert.False(run.Banish(0));
        Assert.True(run.Skip()); Assert.Null(run.Choice);
    }

    [Fact]
    public void OmensRaiseCorruptionAndRewards()
    {
        var plain = NewRun(); var cursed = NewRun(omens: new[] { Omen.Swarm, Omen.Brutality });
        Assert.Equal(50, cursed.Corruption);
        Assert.True(cursed.RewardMultiplier > plain.RewardMultiplier * 1.45f);
    }

    [Fact]
    public void CommitUpdatesProfileOnce()
    {
        var p = new Profile(); var run = NewRun(p);
        run.OnEnemyKilled(EnemyKind.Harbinger);
        var r = run.Results();
        Run.Commit(p, run.Config, r);
        Assert.Equal(1, p.HighestTierCleared); Assert.Equal(r.Shards, p.Shards); Assert.Equal(1, p.RunsWon);
    }
}

public class ProfileTests
{
    [Fact]
    public void BuyRespectsCostRequirementsAndRespecRefunds()
    {
        var p = new Profile { Shards = 1000 };
        Assert.False(p.Buy("secondwind")); // needs vitality 3
        for (int i = 0; i < 3; i++) Assert.True(p.Buy("vitality"));
        Assert.True(p.Buy("secondwind"));
        long spent = 1000 - p.Shards;
        p.Respec();
        Assert.Equal(1000, p.Shards);
        Assert.Empty(p.Nodes);
        Assert.True(spent > 0);
    }

    [Fact]
    public void SaveLoadRoundTripAndBackupRecovery()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lesv_" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "profile.json");
        var p = new Profile { Shards = 77 }; p.Nodes["potency"] = 2; p.Settings.EnemyCap = 300;
        p.Save(path);
        p.Shards = 88; p.Save(path); // creates .bak with 77
        var q = Profile.Load(path);
        Assert.Equal(88, q.Shards); Assert.Equal(2, q.Rank("potency")); Assert.Equal(300, q.Settings.EnemyCap);
        File.WriteAllText(path, "{ corrupt");
        Assert.Equal(77, Profile.Load(path).Shards);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void MetaStatsScaleWithRank()
    {
        var p = new Profile(); p.Nodes["potency"] = 3;
        Assert.Equal(15, p.MetaStats().Where(m => m.Stat == Stat.IncreasedDamage).Sum(m => m.Value), 3);
    }
}

public class RelicTests
{
    [Fact]
    public void RarityControlsAffixCountAndLegendaryHasPower()
    {
        var rng = new Rng(5);
        Assert.Single(RelicCatalog.Roll(rng, RelicSlot.Ring, Rarity.Normal).Mods);
        Assert.Equal(3, RelicCatalog.Roll(rng, RelicSlot.Ring, Rarity.Rare).Mods.Length);
        var leg = RelicCatalog.Roll(rng, RelicSlot.Talisman, Rarity.Legendary);
        Assert.NotEqual(RelicPower.None, leg.Power);
        Assert.Equal(RelicSlot.Talisman, leg.Slot);
    }
}

public class PacingTests
{
    private readonly ITestOutputHelper _out;
    public PacingTests(ITestOutputHelper o) { _out = o; }

    private Simulator.Report Sim(CharacterClass c, int tier, Profile? p = null, ulong seed = 1, params Omen[] omens)
    {
        var rep = Simulator.Simulate(new RunConfig(c, tier, omens, seed), Simulator.CatalogPool(), p ?? new Profile());
        _out.WriteLine($"{c} T{tier} seed{seed}: {rep.Outcome} at {rep.Time / 60:0.0}m, L{rep.Level}, kills {rep.Kills}, build: {rep.Build}");
        foreach (var s in rep.Timeline.Where((_, i) => i % 4 == 0)) _out.WriteLine($"   {s.Time / 60:0.0}m L{s.Level} alive {s.Alive} hp {s.Hp:0} dps {s.Dps:0}");
        return rep;
    }

    [Theory]
    [InlineData(CharacterClass.Mage)]
    [InlineData(CharacterClass.Sentinel)]
    [InlineData(CharacterClass.Primalist)]
    [InlineData(CharacterClass.Acolyte)]
    [InlineData(CharacterClass.Rogue)]
    public void EveryClassCanWinTierOneWithReasonablePicks(CharacterClass c)
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 3; seed++) if (Sim(c, 1, seed: seed).Outcome == RunState.Victory) wins++;
        Assert.True(wins >= 2, $"{c} won only {wins}/3 tier-1 sims");
    }

    [Fact]
    public void LevelPacingIsBrisk()
    {
        var rep = Sim(CharacterClass.Mage, 1);
        Assert.InRange(rep.LevelAt(1), 4, 14);   // several choices in the first minute
        Assert.InRange(rep.LevelAt(5), 15, 45);
        Assert.InRange(rep.Level, 45, 120);
    }

    [Fact]
    public void HighTierWithoutMetaIsLethal()
    {
        int deaths = 0;
        for (ulong seed = 1; seed <= 3; seed++) if (Sim(CharacterClass.Mage, 10, seed: seed, omens: new[] { Omen.Brutality, Omen.Swarm }).Outcome == RunState.Defeat) deaths++;
        Assert.True(deaths >= 2, "Tier X with curses should usually kill an unupgraded character");
    }

    [Fact]
    public void MetaProgressionHelps()
    {
        var p = new Profile();
        foreach (var n in new[] { "vitality", "potency", "celerity", "ward" }) p.Nodes[n] = 5;
        p.Nodes["secondwind"] = 2;
        long plain = 0, meta = 0;
        for (ulong s = 1; s <= 3; s++) { plain += Sim(CharacterClass.Mage, 6, seed: s).Kills; meta += Sim(CharacterClass.Mage, 6, p, s).Kills; }
        Assert.True(meta > plain, $"meta kills {meta} <= plain {plain}");
    }

    [Fact]
    public void HighTiersAreSurvivableForAWhileEvenWhenTheyEventuallyKill()
    {
        // A fresh character at Tier X should not die in the opening; the ramp should give it a few minutes.
        for (ulong seed = 1; seed <= 3; seed++)
            Assert.True(Sim(CharacterClass.Mage, 10, seed: seed).Time > 120, "Tier X should not be an instant death");
    }

    [Fact]
    public void TierTenNeedsMetaProgression()
    {
        int plainWins = 0, metaWins = 0;
        var p = new Profile();
        foreach (var n in MetaCatalog.All) p.Nodes[n.Id] = n.MaxRank;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            if (Sim(CharacterClass.Mage, 10, seed: seed).Outcome == RunState.Victory) plainWins++;
            if (Sim(CharacterClass.Mage, 10, p, seed).Outcome == RunState.Victory) metaWins++;
        }
        Assert.True(plainWins <= 1, $"Tier X won {plainWins}/3 without meta");
        Assert.True(metaWins >= 2, $"Tier X won only {metaWins}/3 with a maxed Obelisk");
    }
}
