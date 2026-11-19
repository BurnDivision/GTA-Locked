using System;
using HarmonyLib;
using Il2Cpp;

namespace LESurvivors.Mod.Game;

/// <summary>Harmony patches. All of them are no-ops unless a Survivors run is active.</summary>
internal static class Patches
{
    /// <summary>Run enemies drop nothing from LE's loot tables and give no LE experience; the mod drops its own motes.</summary>
    [HarmonyPatch(typeof(DeathItemDrop), nameof(DeathItemDrop.OnDeath))]
    internal static class NoLootForRunEnemies
    {
        private static bool Prefix(Dying dying)
        {
            try { return !(RunHost.Active && dying != null && RunHost.IsRunEnemy(dying.gameObject)); }
            catch { return true; }
        }
    }

    /// <summary>Kill tracking for run enemies.</summary>
    [HarmonyPatch(typeof(Dying), nameof(Dying.die))]
    internal static class TrackDeaths
    {
        private static void Postfix(Dying __instance)
        {
            if (!RunHost.Active) return;
            try { RunHost.Instance?.OnActorDied(__instance.gameObject); }
            catch (Exception e) { Log.Warn("death hook: " + e.Message); }
        }
    }

    /// <summary>No LE character experience during a run (the run has its own levels), so the real character is unchanged.</summary>
    [HarmonyPatch(typeof(ExperienceTracker), nameof(ExperienceTracker.GainExpFromEnemyOrMote))]
    internal static class NoCharacterXp
    {
        private static bool Prefix() => !RunHost.Active;
    }

    [HarmonyPatch(typeof(ExperienceTracker), nameof(ExperienceTracker.GainExp))]
    internal static class NoCharacterXp2
    {
        private static bool Prefix() => !RunHost.Active;
    }
}
