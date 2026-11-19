using System.Collections.Generic;
using Il2Cpp;
using LESurvivors.Core;

namespace LESurvivors.Mod.Game;

/// <summary>Maps Core stats onto Last Epoch SP stats. Percent values become fractions.</summary>
internal static class StatBridge
{
    private static AT At(Tag t) => (AT)(int)t;

    private static AilmentID? AilmentOf(Stat s) => s switch
    {
        Stat.IgniteChance => AilmentID.Ignite,
        Stat.ChillChance => AilmentID.Chill,
        Stat.ShockChance => AilmentID.Shock,
        Stat.BleedChance => AilmentID.Bleed,
        Stat.PoisonChance => AilmentID.Poison,
        _ => null,
    };

    /// <summary>Applies the run's global sheet as LE stat buffs (replacing any previous run buffs).</summary>
    public static void ApplyGlobal(StatSheet sheet)
    {
        LE.ClearRunBuffs();
        foreach (var m in sheet.All())
        {
            if (m.Value == 0) continue;
            var tags = At(m.Scope);
            float f = m.Value / 100f;
            switch (m.Stat)
            {
                case Stat.IncreasedDamage: LE.AddBuff(SP.Damage, 0, f, 0, tags); break;
                case Stat.MoreDamage: LE.AddBuff(SP.Damage, 0, 0, f, tags); break;
                case Stat.IncreasedArea: LE.AddBuff(SP.IncreasedAreaForAreaSkills, 0, f, 0, tags); break;
                case Stat.CritChance: LE.AddBuff(SP.CriticalChance, f, 0, 0, tags); break;
                case Stat.CritMulti: LE.AddBuff(SP.CriticalMultiplier, f, 0, 0, tags); break;
                case Stat.AilmentEffect: LE.AddBuff(SP.IncreasedAilmentEffect, 0, f, 0, tags); break;
                case Stat.MoveSpeed: LE.AddBuff(SP.Movespeed, 0, f, 0, AT.None); break;
                case Stat.MaxHealthPct: LE.AddBuff(SP.Health, 0, f, 0, AT.None); break;
                case Stat.AddedHealth: LE.AddBuff(SP.Health, m.Value, 0, 0, AT.None); break;
                case Stat.HealthRegen: LE.AddBuff(SP.HealthRegen, m.Value, 0, 0, AT.None); break;
                case Stat.Armour: LE.AddBuff(SP.Armour, m.Value, 0, 0, AT.None); break;
                case Stat.AllResistance: LE.AddBuff(SP.AllResistances, f, 0, 0, AT.None); break;
                case Stat.DamageReduction: LE.AddBuff(SP.DamageTaken, 0, 0, -f, AT.None); break;
                case Stat.Penetration: LE.AddBuff(SP.Penetration, f, 0, 0, tags); break;
                case Stat.FreezeRate: LE.AddBuff(SP.FreezeRateMultiplier, 0, f, 0, tags); break;
                default:
                    // ailment chances are applied per cast (they need an ailment id); mod-only stats are consumed by RunHost
                    break;
            }
        }
    }

    /// <summary>Per-cast temporary stats for one skill: its own rank-ups, plus global ailment chances.</summary>
    public static List<Stats.Stat> CastStats(Build b, OwnedSkill s)
    {
        var list = new List<Stats.Stat>();
        foreach (var m in s.Local.All())
        {
            float f = m.Value / 100f;
            switch (m.Stat)
            {
                case Stat.IncreasedDamage: list.Add(LE.TempStat(SP.Damage, AT.None, 0, f, 0)); break;
                case Stat.MoreDamage: list.Add(LE.TempStat(SP.Damage, AT.None, 0, 0, f)); break;
                case Stat.IncreasedArea: list.Add(LE.TempStat(SP.IncreasedAreaForAreaSkills, AT.None, 0, f, 0)); break;
                case Stat.CritChance: list.Add(LE.TempStat(SP.CriticalChance, AT.None, f, 0, 0)); break;
            }
        }
        if (s.Ascended) list.Add(LE.TempStat(SP.Damage, AT.None, 0, 0, Build.AscensionMore - 1f));
        foreach (var st in new[] { Stat.IgniteChance, Stat.ChillChance, Stat.ShockChance, Stat.BleedChance, Stat.PoisonChance })
        {
            float v = b.For(s, st);
            var id = AilmentOf(st);
            if (v > 0 && id.HasValue) list.Add(LE.TempAilmentChance(id.Value, v / 100f));
        }
        return list;
    }
}
