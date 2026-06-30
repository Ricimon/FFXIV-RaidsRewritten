using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class PickyDolls : Mechanic
{
    private const uint FLUID_SWING_ACTION_ID = 18864;

    private readonly List<Entity> attacks = [];

    private bool debuffsAssigned = false;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }

        if (set.Action.Value.RowId == FLUID_SWING_ACTION_ID &&
            !debuffsAssigned)
        {
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
    }
}
