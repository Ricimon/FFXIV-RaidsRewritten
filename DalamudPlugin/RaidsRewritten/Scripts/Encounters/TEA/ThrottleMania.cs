using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace RaidsRewritten.Scripts.Encounters.TEA;
public class ThrottleMania : Mechanic
{
    private const uint ESUNA_ACTION_ID = 7568;
    private const uint THROTTLE_STATUS_ID = 700;
    private const uint LIQUID_RAGE_DATA_ID = 0x2C49;

    private const float PROTEAN_SCALE = 40;
    private const float PROTEAN_DURATION_SECONDS = 5;
    private HashSet<Vector3> tornadoPositions = [];

    private readonly List<List<Entity>> attacks = [];

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }
        if (tornadoPositions.Count == 0) { return; }

        List<Entity> toDestruct = [];
        if (set.Action.Value.RowId == ESUNA_ACTION_ID)
        {
            if (set.TargetEffects == null) { return; }

            foreach (TargetEffect effect in set.TargetEffects)
            {
                effect.ForEach(e =>
                {
                    if (e.type == ActionEffectType.RecoveredFromStatusEffect
                    && e.value == THROTTLE_STATUS_ID)
                    {
                        var proteanTarget = Svc.Objects.SearchById(effect.TargetID);
                        if (proteanTarget == null) { return; }
                        if (!tornadoPositions.OrderBy((p) => Vector3.Distance(p, proteanTarget.Position)).TryGetFirst(out var tornadoPos))
                        { return; }

                        var angleToSource = MathUtilities.GetAbsoluteAngleFromSourceToTarget(tornadoPos, proteanTarget.Position);
                        ExecuteCone(tornadoPos, proteanTarget.Position, toDestruct);
                    }
                });
            }
        }
        // handle paeon
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.BaseId != LIQUID_RAGE_DATA_ID) { return; }
        if (tornadoPositions.Count == 3)
        {
            tornadoPositions.Clear();
        }

        tornadoPositions.Add(newObject.Position);
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            Reset();
        }
    }

    public override void Reset()
    {
        for (var i = attacks.Count -1; i >= 0; i--)
        {
            Reset(attacks[i]);
        }
        attacks.Clear();
    }

    private void Reset(List<Entity> toDestruct)
    {
        foreach (var e in toDestruct)
        {
            e.Destruct();
        }

        toDestruct.Clear();
        attacks.Remove(toDestruct);
    }

    public void ExecuteCone(Vector3 sourcePosition, Vector3 target, List<Entity> toDestruct)
    {
        var angle = MathUtilities.GetAbsoluteAngleFromSourceToTarget(sourcePosition, target);
        var delayedAction = DelayedAction.Create(this.World, () => Telegraph(sourcePosition, angle, toDestruct), 0);
        toDestruct.Add(delayedAction);
        delayedAction = DelayedAction.Create(this.World, () => Reset(toDestruct), 5);
        toDestruct.Add(delayedAction);
    }
    
    private void Telegraph(Vector3 position, float rotation, List<Entity> toDestruct)
    {
        if (!SpawnOmen<ProteanOmen>(out Entity proteanOmen, position, rotation, toDestruct)) { return; }

        void DestroyTelegraph()
        {
            toDestruct.Remove(proteanOmen);
            proteanOmen.Destruct();
        }

        var delayedAction = DelayedAction.Create(this.World, DestroyTelegraph, PROTEAN_DURATION_SECONDS);
    }

    private bool SpawnOmen<T>(out Entity Omen, Vector3 startPosition, float rotation, List<Entity> toDestruct)
    {
        if (this.EntityManager.TryCreateEntity<T>(out Entity tempOmen))
        {
            tempOmen.Set(new Position(startPosition))
                .Set(new Rotation(rotation))
                .Set(new Scale(new Vector3(PROTEAN_SCALE)));
            toDestruct.Add(tempOmen);
        } else
        {
            Reset(toDestruct);
            Omen = default;
            return false;
        }
        Omen = tempOmen;
        return true;
    }

}

