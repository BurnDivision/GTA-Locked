using System;
using System.IO;
using LESurvivors.Core;
using LESurvivors.Mod.Dev;
using LESurvivors.Mod.Game;
using LESurvivors.Mod.UI;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(LESurvivors.Mod.SurvivorsMod), "LE Survivors", "0.1.0", "BurnDivision")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace LESurvivors.Mod;

public sealed class SurvivorsMod : MelonMod
{
    internal static SurvivorsMod I { get; private set; }
    internal Profile Profile { get; private set; }
    internal string DataDir { get; private set; }
    internal string ProfilePath => Path.Combine(DataDir, "profile.json");
    internal Screens Screens { get; private set; }
    private DevConsole _dev;

    public override void OnInitializeMelon()
    {
        I = this;
        Log.L = LoggerInstance;
        DataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "LESurvivors");
        Directory.CreateDirectory(DataDir);
        Profile = Profile.Load(ProfilePath, Log.Warn);
        Screens = new Screens(() => Profile, SaveProfile, StartRun);
        _dev = new DevConsole(Path.Combine(DataDir, "dev"), this);
        Log.Info($"LE Survivors loaded. Profile: {Profile.Shards} shards, {Profile.RunsStarted} runs. Press {Profile.Settings.MenuKey} in game.");
    }

    internal void SaveProfile()
    {
        try { Profile.Save(ProfilePath); } catch (Exception e) { Log.Error("profile save failed: " + e.Message); }
    }

    internal void StartRun(RunConfig cfg)
    {
        try
        {
            if (!LE.InGame) { Screens.Toast("Not in game"); return; }
            if (!LE.IsOfflineCharacter) { Screens.Toast("Survivors mode is offline-only"); return; }
            RunHost.Start(cfg, Profile, _ => SaveProfile());
            Screens.MenuOpen = false;
        }
        catch (Exception e) { Log.Error("start run: " + e); Screens.Toast("Could not start: " + e.Message); }
    }

    public override void OnUpdate()
    {
        _dev?.Poll();
        try { RunHost.Instance?.Update(Time.deltaTime); }
        catch (Exception e) { Log.Error("run update: " + e); }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        // leaving the zone ends the run cleanly (the world it lived in is gone)
        var h = RunHost.Instance;
        if (h != null && !h.Finished) { Log.Info("Scene changed during run; abandoning."); h.Abandon(); }
    }

    public override void OnGUI()
    {
        try
        {
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                Screens.FeedKey(e.keyCode);
                if (e.keyCode.ToString() == Profile.Settings.MenuKey && RunHost.Instance == null) Screens.MenuOpen = !Screens.MenuOpen;
                if (e.keyCode.ToString() == Profile.Settings.PauseKey && RunHost.Active && RunHost.Instance.Run.Choice == null)
                {
                    Screens.PauseOpen = !Screens.PauseOpen;
                    RunHost.Instance.Run.SetPaused(Screens.PauseOpen);
                    Time.timeScale = Screens.PauseOpen ? 0f : 1f;
                }
            }
            if (e != null && e.type == EventType.Repaint) Screens.EndFrame();
            Screens.Draw();
        }
        catch (Exception ex) { Log.Error("gui: " + ex.Message); }
    }

    public override void OnApplicationQuit()
    {
        RunHost.Instance?.Cleanup();
        SaveProfile();
    }
}
