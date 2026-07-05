using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class FireTornado : Mechanic
{
    private const uint LIQUID_RAGE_DATA_ID = 0x2C49;
    private const uint DRAINAGE_ACTION_ID = 18471;
    private const uint SPLASH_ACTION_ID = 18866;
    private const uint LIVING_LIQUID_BASE_ID = 0x2C47;

    private const float FirstDonutDelay = 5f;
    private const float SecondDonutDelay = 0.5f;
    private Vector3 ArenaCenter = new(100, 0, 100);

    private readonly List<Entity> attacks = [];

    private List<Vector3> activeTornadoPositions = [];
    private int numSplashes = 0;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        activeTornadoPositions.Clear();
        numSplashes = 0;
    }

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if (source.BaseId == LIVING_LIQUID_BASE_ID &&
            command == 14 &&
            attacks.Count > 0)
        {
            Reset();
        }
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
        if (newObject.BaseId != LIQUID_RAGE_DATA_ID) { return; }
        activeTornadoPositions.Add(newObject.Position);
        if (activeTornadoPositions.Count() == 3)
        {
            var position = ArenaCenter * 4 - activeTornadoPositions[0] - activeTornadoPositions[1] - activeTornadoPositions[2];

            var tornado = FireTornadoEntity.CreateEntity(World)
                .Set(new Position(position));

            attacks.Add(tornado);

            DelayedAction.Create(World, () =>
            {
                if (!tornado.IsValid()) { return; }
                FireTornadoEntity.DonutMech(tornado);
            }, FirstDonutDelay);
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (!set.Action.HasValue) { return; }
        if (attacks.Count == 0) { return; }

        var tornado = attacks[0];
        if (!tornado.IsValid()) { return; }

        switch (set.Action.Value.RowId)
        {
            case SPLASH_ACTION_ID:
                numSplashes++;
                if (numSplashes == 5)
                {
                    DelayedAction.Create(World, () =>
                    {
                        if (!tornado.IsValid()) { return; }
                        FireTornadoEntity.DonutMech(tornado);
                    }, SecondDonutDelay);
                }
                break;
            case DRAINAGE_ACTION_ID:
                if (!tornado.IsValid()) { return; }
                tornado.Destruct();
                break;
        }
    }
}
