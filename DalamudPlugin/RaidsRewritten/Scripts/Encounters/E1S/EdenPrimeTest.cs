using System;
using System.Collections.Generic;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest : IDalamudHook
{
    private readonly List<Mechanic> mechanics;

    public EdenPrimeTest(EncounterManager encounterManager, Mechanic.Factory mechanicFactory)
    {
        this.mechanics = [mechanicFactory.Create<PermanentViceOfApathy>()];

        foreach(var mechanic in this.mechanics)
        {
            encounterManager.AddMechanic(mechanic);
        }
    }

    public void HookToDalamud()
    {
        
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
