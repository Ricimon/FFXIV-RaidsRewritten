using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters;

public interface IEncounter
{
    ushort TerritoryId { get; }

    string Name { get; }

    IEnumerable<Mechanic> GetMechanics();

    void DrawConfig();
}
