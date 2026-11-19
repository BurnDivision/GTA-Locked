using LESurvivors.Core.Content;

namespace LESurvivors.Core;

public enum EnemyKind { Normal, Elite, Warden, Harbinger }

public readonly record struct SpawnRequest(EnemyKind Kind, int Count, float DirectionDeg = float.NaN, int PoolTier = 0);

/// <summary>Multipliers the adapter applies to every spawned enemy (on top of LE's own area-level scaling).</summary>
public readonly record struct EnemyScaling(float Health, float Damage, float Speed, int AreaLevel);

/// <summary>
/// Time-driven spawn director. Pure logic: given elapsed time and the live enemy count it emits spawn requests.
/// </summary>
public sealed class Director
{
    public const float RunLength = 20 * 60f;
    public static readonly float[] WardenTimes = { 5 * 60f, 10 * 60f, 15 * 60f };
    public static readonly float[] SurgeTimes = { 7.5f * 60f, 12.5f * 60f, 17.5f * 60f };
    public const float SurgeDuration = 20f;
    public const float EliteEvery = 60f;

    private readonly Rng _rng;
    private readonly int _tier;
    private readonly HashSet<Omen> _omens;
    private readonly int _cap;
    private float _budget;
    private float _nextElite = EliteEvery;
    private int _wardensSpawned;
    private readonly List<float> _surges;
    private int _surgeIdx;
    private float _surgeEnds = -1, _surgeDir;

    public bool HarbingerSpawned { get; private set; }
    public bool HarbingerAlive { get; set; }
    public bool InSurge(float t) => t < _surgeEnds;
    public void MarkHarbingerDefeated() { HarbingerSpawned = true; HarbingerAlive = false; }

    public Director(Rng rng, int tier, IEnumerable<Omen> omens, int cap)
    {
        _rng = rng; _tier = Math.Clamp(tier, 1, 10); _omens = new HashSet<Omen>(omens); _cap = Math.Clamp(cap, 60, 600);
        _surges = SurgeTimes.ToList();
        if (_omens.Contains(Omen.Ambush)) _surges = _surges.Concat(SurgeTimes.Select(s => s - 150f)).OrderBy(x => x).ToList();
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>Desired number of live enemies at time t.</summary>
    public int TargetAlive(float t)
    {
        float baseCount = 30 + 270 * Smooth(t / RunLength);
        if (t > RunLength) baseCount = 300 + (t - RunLength) / 60f * 12;  // overtime keeps climbing
        if (_omens.Contains(Omen.Swarm)) baseCount *= 1.35f;
        float cap = _cap * (_omens.Contains(Omen.Swarm) ? 1.35f : 1f);
        return (int)Math.Min(baseCount, cap);
    }

    /// <summary>Spawns allowed per second (how fast the field refills).</summary>
    public float SpawnRate(float t) => Math.Max(5f, TargetAlive(t) / 5f);

    /// <summary>The LE enemy pool tier to draw from (0..4, unlocks tougher families over time).</summary>
    public int PoolTier(float t) => Math.Min(4, (int)(t / 240f));

    public EnemyScaling Scaling(float t, EnemyKind kind)
    {
        float minutes = t / 60f;
        float overtime = Math.Max(0, t - RunLength) / 60f;
        // Tier pressure ramps in over the first 5 minutes so high tiers are hard, not instant deaths.
        float ramp = Smooth((t - 90f) / 450f); // tier pressure starts at 1:30 and is full by 9:00
        float hp = (1f + 0.2f * minutes + 0.035f * minutes * minutes) * (1f + 0.4f * (_tier - 1) * ramp) * (1f + 0.08f * overtime) * (1f + 0.08f * overtime);
        float dmg = (0.7f + 0.06f * minutes) * (1f + 0.2f * (_tier - 1) * ramp) * (1f + 0.08f * overtime);
        float spd = 1f + 0.006f * minutes + (_omens.Contains(Omen.Haste) ? 0.15f : 0f);
        if (_omens.Contains(Omen.Brutality)) dmg *= 1.3f;
        float kindHp = kind switch { EnemyKind.Elite => 6f, EnemyKind.Warden => 40f, EnemyKind.Harbinger => 100f, _ => 1f };
        if (kind != EnemyKind.Normal && _omens.Contains(Omen.Tenacity)) kindHp *= 1.4f;
        int areaLevel = Math.Clamp(1 + (int)(minutes * 2.5f) + (_tier - 1) * 6, 1, 100);
        return new EnemyScaling(hp * kindHp, dmg * (kind == EnemyKind.Normal ? 1f : 1.5f), spd, areaLevel);
    }

    /// <summary>Advance the director. Returns the spawns to perform this frame.</summary>
    public List<SpawnRequest> Update(float t, float dt, int liveCount)
    {
        var req = new List<SpawnRequest>();
        int pool = PoolTier(t);

        if (t >= RunLength && !HarbingerSpawned)
        {
            HarbingerSpawned = true; HarbingerAlive = true;
            req.Add(new SpawnRequest(EnemyKind.Harbinger, 1, PoolTier: 4));
            return req;
        }
        if (HarbingerAlive) return req; // trash pauses during the final fight

        if (_wardensSpawned < WardenTimes.Length && t >= WardenTimes[_wardensSpawned])
        {
            req.Add(new SpawnRequest(EnemyKind.Warden, 1, PoolTier: Math.Min(4, pool + 1)));
            _wardensSpawned++;
        }
        if (t >= _nextElite)
        {
            int n = 1 + (int)(t / 400f) + (_tier >= 6 ? 1 : 0);
            req.Add(new SpawnRequest(EnemyKind.Elite, n, PoolTier: pool));
            _nextElite += EliteEvery;
        }
        if (_surgeIdx < _surges.Count && t >= _surges[_surgeIdx])
        {
            _surgeEnds = t + SurgeDuration; _surgeDir = _rng.Range(0f, 360f); _surgeIdx++;
        }

        bool surge = InSurge(t);
        int target = TargetAlive(t) + (surge ? 60 : 0);
        _budget += SpawnRate(t) * (surge ? 3f : 1f) * dt;
        int deficit = Math.Max(0, target - liveCount);
        int n2 = Math.Min(deficit, (int)_budget);
        if (n2 > 0)
        {
            _budget -= n2;
            // spawn in packs so groups arrive together
            while (n2 > 0)
            {
                int pack = Math.Min(n2, _rng.Range(3, 9));
                req.Add(new SpawnRequest(EnemyKind.Normal, pack, surge ? _surgeDir + _rng.Range(-25f, 25f) : float.NaN, pool));
                n2 -= pack;
            }
        }
        _budget = Math.Min(_budget, 40); // do not hoard
        return req;
    }
}
