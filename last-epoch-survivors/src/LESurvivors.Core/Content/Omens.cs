namespace LESurvivors.Core.Content;

public enum Omen { Swarm, Brutality, Haste, Tenacity, Famine, Frailty, Ambush }

public sealed record OmenDef(Omen Id, string Name, string Text, int Corruption);

public static class OmenCatalog
{
    public static readonly OmenDef[] All =
    {
        new(Omen.Swarm, "Omen of the Swarm", "+35% enemy cap and spawn rate", 25),
        new(Omen.Brutality, "Omen of Brutality", "+30% enemy damage", 25),
        new(Omen.Haste, "Omen of Haste", "+15% enemy movement speed", 20),
        new(Omen.Tenacity, "Omen of Tenacity", "+40% elite and boss health", 20),
        new(Omen.Famine, "Omen of Famine", "Health orbs 50% rarer, 30% weaker healing", 15),
        new(Omen.Frailty, "Omen of Frailty", "-20% maximum health", 20),
        new(Omen.Ambush, "Omen of Ambush", "Surges happen twice as often", 15),
    };

    public static OmenDef Get(Omen o) => All.First(x => x.Id == o);
    public static int Corruption(IEnumerable<Omen> omens) => omens.Distinct().Sum(o => Get(o).Corruption);
}
