using System.Text.Json;
using System.Text.Json.Serialization;
using LESurvivors.Core.Content;

namespace LESurvivors.Core;

/// <summary>Persistent meta progress. Stored only in the mod's own folder; never touches LE saves.</summary>
public sealed class Profile
{
    public int Version { get; set; } = 1;
    public long Shards { get; set; }
    public long LifetimeShards { get; set; }
    public Dictionary<string, int> Nodes { get; set; } = new();
    public int HighestTierCleared { get; set; }          // 0 = none, tiers are 1..10
    public Dictionary<string, RecordEntry> Records { get; set; } = new(); // per class
    public int RunsStarted { get; set; }
    public int RunsWon { get; set; }
    public long TotalKills { get; set; }
    public HashSet<string> SeenSkills { get; set; } = new();
    public Settings Settings { get; set; } = new();

    public int Rank(string node) => Nodes.TryGetValue(node, out var r) ? r : 0;

    public int ActiveSlots => MetaCatalog.BaseActiveSlots + Rank("arsenal");
    public int RelicSlots => 3 + Rank("reliquary");
    public bool OmensUnlocked => Rank("epochgate") > 0;
    public int MaxSelectableTier => Math.Min(10, HighestTierCleared + 1);

    public bool CanBuy(string nodeId, out string reason)
    {
        var n = MetaCatalog.Get(nodeId);
        int r = Rank(nodeId);
        if (r >= n.MaxRank) { reason = "Maxed"; return false; }
        if (n.Requires != null && Rank(n.Requires) < n.RequiresRank)
        { reason = $"Requires {MetaCatalog.Get(n.Requires).Name} {n.RequiresRank}"; return false; }
        if (Shards < n.CostOf(r)) { reason = "Not enough Time Shards"; return false; }
        reason = ""; return true;
    }

    public bool Buy(string nodeId)
    {
        if (!CanBuy(nodeId, out _)) return false;
        Shards -= MetaCatalog.Get(nodeId).CostOf(Rank(nodeId));
        Nodes[nodeId] = Rank(nodeId) + 1;
        return true;
    }

    /// <summary>Refund everything (lets players respec the tree).</summary>
    public void Respec()
    {
        foreach (var (id, rank) in Nodes.ToList())
        {
            var n = MetaCatalog.Get(id);
            for (int r = 0; r < rank; r++) Shards += n.CostOf(r);
        }
        Nodes.Clear();
    }

    public IEnumerable<StatMod> MetaStats()
    {
        foreach (var (id, rank) in Nodes)
        {
            var n = MetaCatalog.All.FirstOrDefault(x => x.Id == id);
            if (n == null) continue;
            foreach (var m in n.PerRank) yield return m.Scaled(rank);
        }
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static Profile FromJson(string json) => JsonSerializer.Deserialize<Profile>(json, Json) ?? new Profile();

    /// <summary>Atomic save: write temp, keep previous as .bak, then move into place.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ToJson());
        if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Loads the profile, falling back to the .bak if the main file is missing or corrupt.</summary>
    public static Profile Load(string path, Action<string>? log = null)
    {
        foreach (var p in new[] { path, path + ".bak" })
        {
            try { if (File.Exists(p)) return FromJson(File.ReadAllText(p)); }
            catch (Exception e) { log?.Invoke($"profile {p} unreadable: {e.Message}"); }
        }
        return new Profile();
    }
}

public sealed class RecordEntry
{
    public float BestTime { get; set; }
    public int BestTier { get; set; }
    public long BestKills { get; set; }
    public int BestLevel { get; set; }
}

public enum TargetingOverride { PerSkill, Nearest, Densest, Mouse }

public sealed class Settings
{
    public int EnemyCap { get; set; } = 220;
    public TargetingOverride Targeting { get; set; } = TargetingOverride.PerSkill;
    public bool PauseOnLevelUp { get; set; } = true;
    public float UiScale { get; set; } = 1.0f;
    public bool ShowRarityShapes { get; set; } = true;
    public bool ReducedFlashing { get; set; }
    public bool ShowDamageNumbers { get; set; } = true;
    public string MenuKey { get; set; } = "F6";
    public string PauseKey { get; set; } = "P";
}
