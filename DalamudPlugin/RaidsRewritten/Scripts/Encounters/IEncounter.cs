using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters;

public interface IEncounter
{
    uint TerritoryId { get; }

    string Name { get; }

    IEnumerable<Mechanic> GetMechanics();

    void RefreshMechanics();

    void Unload();

    void IncrementRngSeed();

    void DrawConfig();
}
