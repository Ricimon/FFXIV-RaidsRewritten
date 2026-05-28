using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class TeaRewritten : IEncounter
{
    public uint TerritoryId => 887;

    public string Name => "TEA Rewritten";

    private readonly List<Mechanic> mechanics = [];

    public IEnumerable<Mechanic> GetMechanics()
    {
        return mechanics;
    }

    public void RefreshMechanics()
    {
        Unload();
    }

    public void Unload()
    {
        foreach(var mechanic in mechanics)
        {
            mechanic.Reset();
        }
        mechanics.Clear();
    }

    public void IncrementRngSeed()
    {
    }

    public void DrawConfig()
    {
    }
}
