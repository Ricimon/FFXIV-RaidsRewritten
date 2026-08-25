using System.Collections.Generic;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndNisi : Mechanic
{
    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }
}
