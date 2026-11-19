using System.Collections.Generic;
using UnityEngine;

namespace LESurvivors.Mod.UI;

/// <summary>Minimal IMGUI toolkit using explicit Rects only (no GUILayout), which is the most reliable path under Il2Cpp.</summary>
internal static class Gui
{
    public static float Scale = 1f;
    private static readonly Dictionary<int, Texture2D> _tex = new();
    private static GUIStyle _label, _center, _title, _button, _small, _big;

    public static Texture2D Tex(Color c)
    {
        int key = c.GetHashCode();
        if (_tex.TryGetValue(key, out var t) && t != null) return t;
        t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave;
        _tex[key] = t; return t;
    }

    private static GUIStyle Make(GUIStyle baseStyle, int size, TextAnchor anchor, bool bold = false)
    {
        var s = new GUIStyle(baseStyle);
        s.fontSize = Mathf.RoundToInt(size * Scale);
        s.alignment = anchor; s.richText = true; s.wordWrap = true;
        s.normal.textColor = new Color(0.93f, 0.9f, 0.84f);
        if (bold) s.fontStyle = FontStyle.Bold;
        return s;
    }

    public static void Begin()
    {
        if (_label == null || Mathf.Abs(_label.fontSize - 15 * Scale) > 0.5f)
        {
            _label = Make(GUI.skin.label, 15, TextAnchor.UpperLeft);
            _small = Make(GUI.skin.label, 12, TextAnchor.UpperLeft);
            _center = Make(GUI.skin.label, 15, TextAnchor.MiddleCenter);
            _title = Make(GUI.skin.label, 24, TextAnchor.MiddleCenter, true);
            _big = Make(GUI.skin.label, 40, TextAnchor.MiddleCenter, true);
            _button = Make(GUI.skin.button, 15, TextAnchor.MiddleCenter);
        }
    }

    public static float S(float v) => v * Scale;
    public static Rect R(float x, float y, float w, float h) => new(x, y, w, h);

    public static void Panel(Rect r, float alpha = 0.86f)
    {
        GUI.DrawTexture(r, Tex(new Color(0.06f, 0.05f, 0.07f, alpha)));
        Border(r, new Color(0.55f, 0.45f, 0.28f, 0.9f), 1);
    }

    public static void Border(Rect r, Color c, float w)
    {
        var t = Tex(c);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), t);
        GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), t);
        GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), t);
        GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), t);
    }

    public static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Tex(c));

    public static void Bar(Rect r, float frac, Color fg, Color bg)
    {
        Fill(r, bg); Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fg);
    }

    public static void Label(Rect r, string text) => GUI.Label(r, text, _label);
    public static void Small(Rect r, string text) => GUI.Label(r, text, _small);
    public static void Center(Rect r, string text) => GUI.Label(r, text, _center);
    public static void Title(Rect r, string text) => GUI.Label(r, text, _title);
    public static void Big(Rect r, string text) => GUI.Label(r, text, _big);
    public static bool Button(Rect r, string text) => GUI.Button(r, text, _button);

    public static string Col(string hex, string s) => $"<color={hex}>{s}</color>";
    public static string Time(float t) => $"{(int)(t / 60):00}:{(int)(t % 60):00}";
}
