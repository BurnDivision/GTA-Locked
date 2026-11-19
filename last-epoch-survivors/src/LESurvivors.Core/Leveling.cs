namespace LESurvivors.Core;

public static class Leveling
{
    /// <summary>XP needed to go from level n to n+1 (n starts at 1).</summary>
    public static float XpToNext(int level) => 5f + 3f * level + 0.12f * level * level;
    public static float TotalXpFor(int level) { float t = 0; for (int i = 1; i < level; i++) t += XpToNext(i); return t; }

    /// <summary>Base stat growth granted every level (keeps a level-1 LE character viable as enemies scale).</summary>
    public static IEnumerable<StatMod> PerLevel()
    {
        yield return new StatMod(Stat.AddedHealth, 9);
        yield return new StatMod(Stat.IncreasedDamage, 2);
        yield return new StatMod(Stat.Armour, 6);
    }
}
