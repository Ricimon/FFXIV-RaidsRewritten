using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class DreadknightOnNeurolink : Mechanic
{
    private const uint NeurolinkDataId = 0x1E88FF;
    private readonly Vector3 ArenaCenter = new(0,0,0);
    private const int GenerateId = 9902;
    private const int HatchId = 9903;
    private const float SpeedIncrement = 0.5f;


    private readonly List<uint> actionIds = [
        7538,  // interject
        7551,  // head graze
        7554,  // leg graze
        7540,  // low blow
        25880, // sleep
        16560, // repose
    ]; 

    private Entity? attack;

    private int neurolinksSpawned = 0;
    private int oviformsOnField = 0;

    public override void Reset()
    {
        SoftReset();
        neurolinksSpawned = 0;
    }

    private void SoftReset()
    {
        this.attack?.Destruct();
        this.attack = null;
        oviformsOnField = 0;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NeurolinkDataId) { return; }
        neurolinksSpawned++;
        switch (neurolinksSpawned)
        {
            case 1:
                if (attack.HasValue)
                {
                    attack.Value.Destruct();
                    attack = null;
                }

                if (this.AttackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
                {
                    Dalamud.ToastGui.ShowNormal("The Dreadknight seeks signs of resistance...");
                    dreadknight.Set(new Position(ArenaCenter));
                    attack = dreadknight;
                }
                break;
            case 2:
                break;
            case 3:
                SoftReset();
                break;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!attack.HasValue) { return; }

        if (set.Action.Value.RowId == GenerateId && neurolinksSpawned < 3)
        {
            oviformsOnField = neurolinksSpawned;
        } else if (set.Action.Value.RowId == HatchId) {
            oviformsOnField--;
            if (oviformsOnField <= 0 && attack.HasValue && neurolinksSpawned < 3)
            {
                Dreadknight.IncrementSpeed(attack.Value, SpeedIncrement);
            }
        } else if (actionIds.Contains(set.Action.Value.RowId)) {
            if (Dreadknight.HasTarget(attack.Value)) { return; }
            if (set.Source == null) { return; }

            Dreadknight.ApplyTarget(attack.Value, set.Source);
        }
    }

}
