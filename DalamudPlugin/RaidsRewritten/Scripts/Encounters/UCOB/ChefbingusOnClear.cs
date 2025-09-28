using System;
using System.Numerics;
using ECommons.Hooks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class ChefbingusOnClear : Mechanic
{
    // No Reset method as we don't want carby to go away when messing with the mechanic menu

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Complete)
        {
            if (this.EntityManager.TryCreateEntity<Chefbingus>(out var carby))
            {
                carby.Set(new Position(new Vector3(-14f, 0, 27.7f)))
                    .Set(new Rotation(0.82f * MathF.PI));
            }
        }
    }
}
