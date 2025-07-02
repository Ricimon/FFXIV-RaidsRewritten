using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest : IEncounter
{
    public ushort TerritoryId => 853;

    private readonly List<Mechanic> mechanics;

    public EdenPrimeTest(Mechanic.Factory mechanicFactory)
    {
        this.mechanics = [mechanicFactory.Create<PermanentViceOfApathyTest>()];
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }
}
