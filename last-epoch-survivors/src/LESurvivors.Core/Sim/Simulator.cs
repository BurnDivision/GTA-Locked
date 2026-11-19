using LESurvivors.Core.Content;

namespace LESurvivors.Core.Sim;

/// <summary>
/// Headless pacing model of a run. It does NOT reproduce Last Epoch's combat maths. It approximates
/// throughput (damage vs enemy health) and pressure (live enemies vs survivability) so that the curves
/// in Director/Leveling/Offers can be regression-tested. The in-game balance pass refines the constants.
/// </summary>
public sealed class Simulator
{
    public sealed record Sample(float Time, int Level, int Alive, long Kills, float Hp, float Dps);
    public sealed class Report
    {
        public RunState Outcome; public float Time; public int Level; public long Kills;
        public List<Sample> Timeline = new(); public string Build = ""; public RunResults? Results;
        public int LevelAt(float minutes) => Timeline.LastOrDefault(s => s.Time <= minutes * 60)?.Level ?? 1;
    }

    public const float BaseEnemyHp = 30f;     // normal enemy at 0:00, tier 1
    public const float BasePlayerHp = 220f;
    public const float BaseEnemyDps = 0.9f;   // per enemy in contact

    public static Report Simulate(RunConfig cfg, IReadOnlyList<SkillDef> pool, Profile profile, Func<List<Card>, Build, int>? picker = null, float maxTime = Director.RunLength + 240)
    {
        var run = new Run(cfg, pool, profile);
        picker ??= GreedyPicker;
        var rep = new Report();
        var alive = new List<(EnemyKind kind, float hp)>();
        float hp = 0, maxHp = 0;
        void RefreshHp() { float m = (BasePlayerHp + run.Build.Global.Get(Stat.AddedHealth)) * (1 + run.Build.Global.Get(Stat.MaxHealthPct) / 100f); hp += m - maxHp; maxHp = m; }
        RefreshHp(); hp = maxHp;
        run.StatsChanged += RefreshHp;
        run.HealRequested += f => hp = Math.Min(maxHp, hp + maxHp * f);
        run.SpawnRequested += r =>
        {
            var sc = run.Director.Scaling(run.Time, r.Kind);
            for (int i = 0; i < r.Count; i++) alive.Add((r.Kind, BaseEnemyHp * sc.Health));
        };
        var timers = new Dictionary<OwnedSkill, float>();
        const float dt = 0.1f; float nextSample = 0;
        while (run.IsActive && run.Time < maxTime)
        {
            while (run.Choice != null)
            {
                if (run.Choice.Relic != null) run.ResolveRelic(true);
                run.Choose(Math.Clamp(picker(run.Choice.Cards, run.Build), 0, run.Choice.Cards.Count - 1));
            }
            run.Tick(dt, alive.Count);
            // ---- player damage ----
            float dps = 0;
            foreach (var s in run.Build.Skills)
            {
                timers.TryGetValue(s, out var tm); tm -= dt;
                float perCast = s.Def.BaseDamage * run.Build.DamageMultiplier(s) * (1 + Math.Min(1f, run.Build.For(s, Stat.CritChance) / 100f) * (0.5f + run.Build.For(s, Stat.CritMulti) / 100f));
                float area = 1 + run.Build.For(s, Stat.IncreasedArea) / 100f;
                float hits = s.Def.Targeting is Targeting.Self or Targeting.Densest ? 1 + alive.Count / 25f * area : 1.3f + alive.Count / 90f * area;
                hits *= 1 + 0.15f * run.Build.For(s, Stat.IgniteChance) / 100f + 0.1f * run.Build.For(s, Stat.AilmentEffect) / 100f;
                dps += perCast * hits * run.Build.CastsPerTrigger(s) / run.Build.IntervalOf(s);
                while (tm <= 0)
                {
                    tm += run.Build.IntervalOf(s);
                    for (int c = 0; c < run.Build.CastsPerTrigger(s); c++)
                    {
                        float pool2 = perCast * hits; s.Casts++; s.DamageDealt += pool2;
                        // distribute damage over the nearest enemies
                        for (int k = 0; k < alive.Count && pool2 > 0; k++)
                        {
                            var e = alive[k]; float d = Math.Min(e.hp, pool2 / Math.Max(1, hits - k));
                            e.hp -= d; pool2 -= d; alive[k] = e;
                        }
                    }
                }
                timers[s] = tm;
            }
            for (int k = alive.Count - 1; k >= 0; k--)
            {
                if (alive[k].hp > 0.01f) continue;
                var kind = alive[k].kind; alive.RemoveAt(k);
                foreach (var d in run.OnEnemyKilled(kind))
                    if (d.Kind != DropKind.Lodestone && d.Kind != DropKind.TimeFracture) run.Collect(d);
            }
            // ---- pressure ----
            var sc0 = run.Director.Scaling(run.Time, EnemyKind.Normal);
            int contact = Math.Min(alive.Count, 4 + alive.Count / 12);
            float armourDr = run.Build.Global.Get(Stat.Armour) / (run.Build.Global.Get(Stat.Armour) + 400f);
            float dr = Math.Min(0.75f, armourDr * 0.6f + run.Build.Global.Get(Stat.DamageReduction) / 100f + run.Build.Global.Get(Stat.AllResistance) / 300f);
            float incoming = contact * BaseEnemyDps * sc0.Damage * (1 - dr) * (alive.Any(a => a.kind != EnemyKind.Normal) ? 1.5f : 1f);
            hp += (run.Build.Global.Get(Stat.HealthRegen) - incoming) * dt;
            hp = Math.Min(hp, maxHp);
            if (hp <= 0 && !run.OnPlayerDied()) break;
            if (hp <= 0) hp = maxHp;
            if (run.Time >= nextSample) { rep.Timeline.Add(new Sample(run.Time, run.Level, alive.Count, run.Kills, hp, dps)); nextSample += 30; }
        }
        rep.Results = run.Results();
        rep.Outcome = rep.Results.Outcome; rep.Time = run.Time; rep.Level = run.Level; rep.Kills = run.Kills; rep.Build = run.Build.Summary();
        return rep;
    }

    /// <summary>A reasonable player: ascends, takes damage/frequency/multicast and synergy, keeps health up.</summary>
    public static int GreedyPicker(List<Card> cards, Build b)
    {
        double Score(Card c) => c.Kind switch
        {
            CardKind.Ascend => 100,
            CardKind.NewSkill => b.Skills.Count < 3 ? 40 : 18,
            CardKind.SkillRank => (c.Line switch { SkillLine.Multicast => 30, SkillLine.Damage => 16, SkillLine.Frequency => 15, SkillLine.Area => 12, _ => 8 }) * (1 + (int)c.Rarity * 0.5),
            CardKind.NewGlyph => 14 + (c.SubjectId == "vitality" ? 6 : 0),
            CardKind.GlyphRank => 11 * (1 + (int)c.Rarity * 0.4),
            CardKind.Heal => 3,
            _ => 1,
        };
        int best = 0;
        for (int i = 1; i < cards.Count; i++) if (Score(cards[i]) > Score(cards[best])) best = i;
        return best;
    }

    /// <summary>A pool of skill defs for every class, from the curated catalog, for tests and tooling.</summary>
    public static List<SkillDef> CatalogPool()
    {
        var classOf = new Dictionary<string, (CharacterClass c, Tag t)>
        {
            ["Fireball"] = (CharacterClass.Mage, Tag.Fire | Tag.Spell), ["Lightning Blast"] = (CharacterClass.Mage, Tag.Lightning | Tag.Spell),
            ["Glacier"] = (CharacterClass.Mage, Tag.Cold | Tag.Spell), ["Elemental Nova"] = (CharacterClass.Mage, Tag.Fire | Tag.Cold | Tag.Lightning | Tag.Spell),
            ["Frost Claw"] = (CharacterClass.Mage, Tag.Cold | Tag.Spell), ["Volcanic Orb"] = (CharacterClass.Mage, Tag.Fire | Tag.Spell),
            ["Meteor"] = (CharacterClass.Mage, Tag.Fire | Tag.Spell), ["Static Orb"] = (CharacterClass.Mage, Tag.Lightning | Tag.Spell),
            ["Hammer Throw"] = (CharacterClass.Sentinel, Tag.Physical | Tag.Throwing), ["Smite"] = (CharacterClass.Sentinel, Tag.Fire | Tag.Lightning | Tag.Spell),
            ["Judgement"] = (CharacterClass.Sentinel, Tag.Physical | Tag.Fire | Tag.Melee), ["Javelin"] = (CharacterClass.Sentinel, Tag.Physical | Tag.Lightning | Tag.Throwing),
            ["Rive"] = (CharacterClass.Sentinel, Tag.Physical | Tag.Melee), ["Shield Throw"] = (CharacterClass.Sentinel, Tag.Physical | Tag.Throwing),
            ["Tornado"] = (CharacterClass.Primalist, Tag.Physical | Tag.Spell), ["Upheaval"] = (CharacterClass.Primalist, Tag.Physical | Tag.Melee),
            ["Gathering Storm"] = (CharacterClass.Primalist, Tag.Lightning | Tag.Melee), ["Avalanche"] = (CharacterClass.Primalist, Tag.Cold | Tag.Physical | Tag.Spell),
            ["Earthquake"] = (CharacterClass.Primalist, Tag.Physical | Tag.Melee), ["Warcry"] = (CharacterClass.Primalist, Tag.Physical),
            ["Hungering Souls"] = (CharacterClass.Acolyte, Tag.Necrotic | Tag.Spell), ["Rip Blood"] = (CharacterClass.Acolyte, Tag.Physical | Tag.Spell),
            ["Bone Curse"] = (CharacterClass.Acolyte, Tag.Physical | Tag.Curse), ["Marrow Shards"] = (CharacterClass.Acolyte, Tag.Physical | Tag.Spell),
            ["Spirit Plague"] = (CharacterClass.Acolyte, Tag.Necrotic | Tag.DoT), ["Chaos Bolts"] = (CharacterClass.Acolyte, Tag.Fire | Tag.Necrotic | Tag.Spell),
            ["Death Seal"] = (CharacterClass.Acolyte, Tag.Necrotic | Tag.Spell),
            ["Shurikens"] = (CharacterClass.Rogue, Tag.Physical | Tag.Throwing), ["Umbral Blades"] = (CharacterClass.Rogue, Tag.Physical | Tag.Throwing),
            ["Multishot"] = (CharacterClass.Rogue, Tag.Physical | Tag.Bow), ["Hail of Arrows"] = (CharacterClass.Rogue, Tag.Physical | Tag.Bow),
            ["Cinder Strike"] = (CharacterClass.Rogue, Tag.Fire | Tag.Melee), ["Detonating Arrow"] = (CharacterClass.Rogue, Tag.Fire | Tag.Bow),
            ["Acid Flask"] = (CharacterClass.Rogue, Tag.Poison | Tag.Throwing),
        };
        return classOf.Select(kv => SkillCatalog.Build(kv.Key, kv.Value.c, kv.Value.t, 0)).ToList();
    }
}
