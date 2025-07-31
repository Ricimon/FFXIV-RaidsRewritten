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
    private readonly Vector3 ArenaCenter = new(100,0,100);

    private Entity? attack;

    private bool alreadySpawned = false;

    public override void Reset()
    {
        this.attack?.Destruct();
        this.attack = null;
        alreadySpawned = false;
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
        if (alreadySpawned) { return; }

        if (attack.HasValue)
        {
            attack.Value.Destruct();
            attack = null;
        }

        if (this.AttackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
        {
            dreadknight.Set(new Position(ArenaCenter));
            attack = dreadknight;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!attack.HasValue) { return; }
        if (set.Action.Value.RowId != 25767) { return; }
        if (Dreadknight.HasTarget(attack.Value)) { return; }
        if (set.Source == null) { return; }

        Dreadknight.ApplyTarget(attack.Value, set.Source);
    }

}
