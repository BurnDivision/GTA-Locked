using MelonLoader;

namespace LESurvivors.Mod;

internal static class Log
{
    internal static MelonLogger.Instance L;
    public static void Info(string s) => L?.Msg(s);
    public static void Warn(string s) => L?.Warning(s);
    public static void Error(string s) => L?.Error(s);
}
