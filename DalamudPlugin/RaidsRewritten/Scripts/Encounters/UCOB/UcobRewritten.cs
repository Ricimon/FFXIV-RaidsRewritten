using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public sealed class UcobRewritten : IEncounter
{
    public ushort TerritoryId => 0;

    private readonly List<Mechanic> mechanics;

    public UcobRewritten(Mechanic.Factory mechanicFactory)
    {
        this.mechanics = [mechanicFactory.Create<PermanentTwister>()];
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }
}
