using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class LiquidHeaven : Mechanic
{
    private const uint NEUROLINK_DATA_ID = 0x1E88FF;
    //private const uint CLEANSE_DATA_ID = 0x1E91D4;

    private readonly List<Entity> attacks = [];
    private const int MaxHeavens = 3;

    private int heavensSpawned;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        this.heavensSpawned = 0;
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
    //"bgcommon/world/common/vfx_for_btl/b3566/eff/b3566_rset_y1.avfx"
    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId == NEUROLINK_DATA_ID)
        {
            if (heavensSpawned >= MaxHeavens) { return; }

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
            heavensSpawned++;
        }
        /*
        if (newObject.DataId == CLEANSE_DATA_ID)
        {
            var player = this.Dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.AttackManager.TryCreateAttackEntity<Attacks.DoomCleanse>(out var dc))
                { 
                    dc.Set(new Position(newObject.Position))
                        .Set(new Rotation(newObject.Rotation))
                        .Set(new DoomCleanse.Component(newObject));
                    attacks.Add(dc);
                }
            }
        }
        */
    }
}
