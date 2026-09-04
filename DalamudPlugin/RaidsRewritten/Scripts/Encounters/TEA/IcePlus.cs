using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class IcePlus : Mechanic
{
    public int RngSeed { get; set; }

    private const uint GelidGaolBaseId = 0x2C81;
    private const uint PropellerWindActionId = 18482;

    private readonly List<Entity> attacks = [];

    private Vector3? icePosition;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        icePosition = null;
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
        if (newObject != null && newObject.BaseId == GelidGaolBaseId)
        {
            icePosition = newObject.Position;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId == PropellerWindActionId)
        {
            if (icePosition == null) { return; }

            var seed = RngSeed;
            unchecked
            {
                seed += 0x1CE;
            }
            var random = new Random(seed);
            var rotationOffset = random.Next() == 0 ? 0 : 0.25f * MathF.PI;

            if (EntityManager.TryCreateEntity<LineOmen>(out var line1))
            {
                line1.Set(new Position(icePosition.Value));
                line1.Set(new Scale(new Vector3(3, 1, 50)));
                line1.Set(new Rotation(rotationOffset));
                line1.Set(new OmenDuration(3f, true));
                attacks.Add(line1);
            }
            if (EntityManager.TryCreateEntity<LineOmen>(out var line2))
            {
                line2.Set(new Position(icePosition.Value));
                line2.Set(new Rotation(0.5f * MathF.PI + rotationOffset));
                line2.Set(new Scale(new Vector3(3, 1, 50)));
                line2.Set(new OmenDuration(3f, true));
                attacks.Add(line2);
            }
        }
    }
}
