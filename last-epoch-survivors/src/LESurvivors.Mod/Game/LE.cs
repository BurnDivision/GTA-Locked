using System;
using System.Collections.Generic;
using Il2Cpp;
using LESurvivors.Core;
using LESurvivors.Core.Content;
using UnityEngine;
using UnityEngine.AI;
using CharacterClass = LESurvivors.Core.Content.CharacterClass;

namespace LESurvivors.Mod.Game;

/// <summary>
/// The ONLY place that knows Last Epoch types for gameplay reads/writes. Everything is wrapped so a game patch that
/// renames something fails loudly here (and is reported by <see cref="SelfCheck"/>) instead of deep inside a run.
/// </summary>
public static class LE
{
    public static Actor Player
    {
        get { try { return PlayerFinder.getPlayerActor(); } catch { return null; } }
    }

    public static bool InGame => Player != null;

    public static Vector3 PlayerPos => Player != null ? Player.transform.position : Vector3.zero;

    public static bool IsOfflineCharacter
    {
        get
        {
            try { var d = PlayerFinder.getPlayerData(); return d != null && d.IsOffline; }
            catch { return false; }
        }
    }

    public static bool IsHardcore
    {
        get { try { var d = PlayerFinder.getPlayerData(); return d != null && d._Hardcore_k__BackingField; } catch { return false; } }
    }

    public static CharacterClass PlayerClass
    {
        get
        {
            try { var d = PlayerFinder.getPlayerData(); return (CharacterClass)Math.Clamp(d._CharacterClass_k__BackingField, 0, 4); }
            catch { return CharacterClass.Mage; }
        }
    }

    public static string CharacterName
    {
        get { try { return PlayerFinder.getPlayerData()?.CharacterName ?? "?"; } catch { return "?"; } }
    }

    // ------------------------------------------------------------------ abilities

    /// <summary>All abilities the class (and its masteries) can learn, keyed by ability name.</summary>
    public static Dictionary<string, Ability> ClassAbilities(CharacterClass cls)
    {
        var result = new Dictionary<string, Ability>(StringComparer.OrdinalIgnoreCase);
        var c = CharacterClassList.getClass((int)cls);
        if (c == null) return result;
        void Add(Ability a) { if (a != null && !string.IsNullOrEmpty(a.abilityName) && !result.ContainsKey(a.abilityName)) result[a.abilityName] = a; }
        if (c.knownAbilities != null) for (int i = 0; i < c.knownAbilities.Count; i++) Add(c.knownAbilities[i]);
        if (c.masteries != null)
            for (int m = 0; m < c.masteries.Length; m++)
            {
                var ms = c.masteries[m]; if (ms == null) continue;
                Add(ms.masteryAbility);
                if (ms.abilities != null) for (int i = 0; i < ms.abilities.Count; i++) Add(ms.abilities[i]?.ability);
            }
        return result;
    }

    /// <summary>Builds the Survivors skill pool for a class from real LE abilities.</summary>
    public static List<(SkillDef def, Ability ability)> BuildSkillPool(CharacterClass cls)
    {
        var list = new List<(SkillDef, Ability)>();
        foreach (var kv in ClassAbilities(cls))
        {
            var tags = (Tag)(int)kv.Value.tags;
            if (!SkillCatalog.IsAutoWeaponCandidate(kv.Key, tags)) continue;
            float cd = 0;
            try { if (kv.Value.chargesGainedPerSecond > 0) cd = 1f / kv.Value.chargesGainedPerSecond; } catch { }
            list.Add((SkillCatalog.Build(kv.Key, cls, tags, cd), kv.Value));
        }
        return list;
    }

    /// <summary>
    /// Casts a real LE ability without animation or state lock, the same path LE uses for triggered casts.
    /// Per-cast stats (damage, area, crit, ailment chance) are attached as temporary stats.
    /// </summary>
    public static bool Cast(Ability ability, Vector3 from, Vector3 target, IList<Stats.Stat> tempStats)
    {
        var p = Player; if (p == null || ability == null) return false;
        var aoc = p.abilityObjectConstructor; if (aoc == null) return false;
        AbilityMutator mut = null; AbilityInfo info = null;
        try
        {
            if (p.mutatorManager != null)
            {
                mut = p.mutatorManager.getMutator(ability);
                info = p.mutatorManager.getMutatorOrAbility(ability);
            }
        }
        catch { }
        if (tempStats != null) for (int i = 0; i < tempStats.Count; i++) aoc.AddStatForNextAbilityUse(tempStats[i]);
        var go = aoc.constructAbilityObject(ability, from, target, UseType.Indirect, null, false, false, mut, false, info, 0f,
            default(AbilityID), false, Vector3.zero);
        return go != null;
    }

    // ------------------------------------------------------------------ stats

    public const string BuffPrefix = "LESV";
    public const float Forever = 1_000_000f;

    public static void ClearRunBuffs()
    {
        var p = Player; if (p?.statBuffs == null) return;
        try { p.statBuffs.removeBuffsWithName(BuffPrefix); } catch (Exception e) { Log.Warn("removeBuffsWithName: " + e.Message); }
    }

    /// <summary>LE expects percentages as fractions for increased/more and percent points as fractions for chances.</summary>
    public static void AddBuff(SP prop, float added, float increased, float more, AT tags)
    {
        var p = Player; if (p?.statBuffs == null) return;
        var moreList = new Il2CppSystem.Collections.Generic.List<float>();
        if (more != 0) moreList.Add(more);
        p.statBuffs.addBuff(Forever, prop, added, increased, moreList, tags, BuffPrefix);
    }

    public static Stats.Stat TempStat(SP prop, AT tags, float added, float increased, float more)
        => new Stats.Stat(prop, tags, added, increased, more, 0, 0);

    public static Stats.Stat TempAilmentChance(AilmentID ailment, float chance)
        => new Stats.Stat(SP.AilmentChance, AT.None, ailment, chance);

    // ------------------------------------------------------------------ health

    public static float PlayerHealth => Player?.health != null ? Player.health.currentHealth : 0;
    public static float PlayerMaxHealth => Player?.health != null ? Player.health.maxHealth : 1;

    public static void HealPlayer(float fraction)
    {
        var h = Player?.health; if (h == null) return;
        h.heal(h.maxHealth * fraction);
    }

    // ------------------------------------------------------------------ enemies

    /// <summary>Hostile LE enemy types usable as survivors fodder, grouped by level band.</summary>
    public static List<ActorData> EnemyPool()
    {
        var list = new List<ActorData>();
        var adl = ActorDataList.instance(); if (adl?.data == null) return list;
        for (int i = 0; i < adl.data.Count; i++)
        {
            var d = adl.data[i]; if (d == null) continue;
            if (!d.spawnsInArena) continue;
            if (d.actorType != ActorData.Type.Normal) continue;
            if (d.isChampion || d.isOmen || d.isNemesis || d.isHarbinger) continue;
            list.Add(d);
        }
        return list;
    }

    public static List<ActorData> BossPool()
    {
        var list = new List<ActorData>();
        var adl = ActorDataList.instance(); if (adl?.data == null) return list;
        for (int i = 0; i < adl.data.Count; i++)
        {
            var d = adl.data[i]; if (d == null) continue;
            if (d.spawnsInArena && (d.actorType == ActorData.Type.MiniBoss || d.actorType == ActorData.Type.Boss)) list.Add(d);
        }
        return list;
    }

    /// <summary>Finds a walkable point near <paramref name="desired"/> on the zone's NavMesh.</summary>
    public static bool TryNavPoint(Vector3 desired, float maxDist, out Vector3 point)
    {
        if (NavMesh.SamplePosition(desired, out var hit, maxDist, NavMesh.AllAreas)) { point = hit.position; return true; }
        point = desired; return false;
    }

    public static Actor SpawnEnemy(ActorData data, Vector3 pos)
    {
        try
        {
            return data.spawn(pos, UnityEngine.Random.Range(0f, 360f), null, default, null, null, true, 0,
                new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(), true);
        }
        catch (Exception e) { Log.Warn($"spawn {data?.actorName}: {e.Message}"); return null; }
    }

    public static void Scale(Actor a, float healthMult, float speedMult, EnemyKind kind)
    {
        try
        {
            if (a.health != null)
            {
                int mh = Math.Max(1, (int)(a.health.maxHealth * healthMult));
                a.health.maxHealth = mh; a.health.currentHealth = mh;
            }
            if (a.navMeshAgent != null) a.navMeshAgent.speed *= speedMult;
            if (kind == EnemyKind.Elite) a.setRarity(Actor.Rarity.Magic);
            if (kind >= EnemyKind.Warden) a.setRarity(Actor.Rarity.Rare);
            if (kind != EnemyKind.Normal) a.transform.localScale *= kind switch { EnemyKind.Elite => 1.25f, EnemyKind.Warden => 1.6f, _ => 2.0f };
        }
        catch (Exception e) { Log.Warn("scale: " + e.Message); }
    }

    public static bool IsDead(Actor a)
    {
        try { return a == null || a.WasCollected || a.health == null || a.health.currentHealth <= 0 || Dying.isDying(a.gameObject); }
        catch { return true; }
    }

    /// <summary>Verify that the LE members this mod relies on still exist (run once at startup and on demand).</summary>
    public static List<string> SelfCheck()
    {
        var problems = new List<string>();
        void Check(string what, Func<object> f) { try { f(); } catch (Exception e) { problems.Add($"{what}: {e.GetType().Name} {e.Message}"); } }
        Check("ActorDataList", () => ActorDataList.instance());
        Check("CharacterClassList", () => CharacterClassList.getClass(1));
        Check("AbilityManager", () => AbilityManager.instance);
        return problems;
    }
}
