using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters;

public interface IEncounter
{
    ushort TerritoryId { get; }

    IEnumerable<Mechanic> GetMechanics();
}
