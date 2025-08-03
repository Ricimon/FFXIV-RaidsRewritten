using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class LiquidHeaven : Mechanic
{
    private const uint NEUROLINK_DATA_ID = 0x1E88FF;

    private readonly List<Entity> attacks = [];

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

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NEUROLINK_DATA_ID) { return; }
        
        var player = this.Dalamud.ClientState.LocalPlayer;
        if (player != null)
        {
            if (this.AttackManager.TryCreateAttackEntity<Attacks.LiquidHeaven>(out var liquidHeaven))
            {
                liquidHeaven.Set(new Position(newObject.Position));
                liquidHeaven.Set(new Rotation(newObject.Rotation));
                attacks.Add(liquidHeaven);
            }
        }
    }
}
