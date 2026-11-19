namespace LESurvivors.Core;

/// <summary>Upgrade rarity, named after Last Epoch's item rarities.</summary>
public enum Rarity { Normal = 0, Magic = 1, Rare = 2, Exalted = 3, Legendary = 4 }

public static class Rarities
{
    public static readonly double[] BaseWeight = { 60, 26, 10, 3.5, 0.5 };
    public static readonly float[] Magnitude = { 1.0f, 1.5f, 2.25f, 3.25f, 5.0f };
    public static readonly string[] Colour = { "#E6E6E6", "#4C8DFF", "#F2D14B", "#B45CFF", "#FF8A2B" };
    /// <summary>Shape glyph shown next to colour for colour-blind accessibility.</summary>
    public static readonly string[] Shape = { "○", "◇", "△", "☆", "✦" };

    public static float Mag(this Rarity r) => Magnitude[(int)r];

    /// <summary>
    /// Luck shifts weight from Normal into higher tiers. Each point of luck moves 0.4% of Normal's weight,
    /// split 55/28/13/4 across Magic..Legendary. Luck is clamped to [0, 200].
    /// </summary>
    public static double[] Weights(float luck, Rarity minimum = Rarity.Normal)
    {
        luck = Math.Clamp(luck, 0, 200);
        var w = (double[])BaseWeight.Clone();
        double moved = w[0] * Math.Min(0.8, luck * 0.004);
        w[0] -= moved;
        w[1] += moved * 0.55; w[2] += moved * 0.28; w[3] += moved * 0.13; w[4] += moved * 0.04;
        for (int i = 0; i < (int)minimum; i++) w[i] = 0;
        return w;
    }

    public static Rarity Roll(Rng rng, float luck, Rarity minimum = Rarity.Normal)
        => (Rarity)Math.Max(0, rng.PickWeighted(Weights(luck, minimum)));
}
