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

    private readonly List<Entity> attacks = [];
    private readonly IReadOnlyList<Vector3> tornadoPositions1 =
        [new(85, 0, 100),
        new(115, 0, 100),
        new(100, 0, 85),
        new(100, 0, 115)];
    private readonly IReadOnlyList<Vector3> tornadoPositions2 =
        [new(89.3934f, 0, 110.6066f),
        new(110.6066f, 0, 110.6066f),
        new(110.6066f, 0, 89.39339f),
        new(89.39339f, 0, 89.3934f)];

    private HashSet<Vector3> availableTornadoPositions = [];
    private int tornadosSpawned = 0;
    private int numSplashes = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        tornadosSpawned = 0;
        numSplashes = 0;
        availableTornadoPositions.Clear();
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
        if (tornadosSpawned > 0) { return; }

        if (availableTornadoPositions.Count == 0)
        {
            if (tornadoPositions1.Contains(newObject.Position))
            {
                availableTornadoPositions = [.. tornadoPositions1];
            }
            else
            {
                availableTornadoPositions = [.. tornadoPositions2];
            }
        }

        availableTornadoPositions.Remove(newObject.Position);

        if (availableTornadoPositions.Count == 1)
        {
            var position = availableTornadoPositions.ElementAt(0);
            availableTornadoPositions.Clear();

            tornadosSpawned++;
            if (!EntityManager.TryCreateEntity<FireTornadoEntity>(out var tornado)) { return; }
            tornado.Set(new Position(position));
            attacks.Add(tornado);

            var action1 = DelayedAction.Create(World, () =>
            {
                if (!tornado.IsValid()) { return; }
                var donut = FireTornadoEntity.DonutMech(tornado);
                attacks.Add(donut);

                var action2 = DelayedAction.Create(World, () =>
                {
                    if (!tornado.IsValid()) { return; }
                    FireTornadoEntity.NetworkedAttack1(tornado, typeof(FireTornadoEntity.NetworkedAttack1Trigger).FullName!);
                }, FireTornadoEntity.Donut.OmenDuration).ChildOf(tornado);
                attacks.Add(action2);
            }, FirstDonutDelay);
            attacks.Add(action1);
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
                    var action1 = DelayedAction.Create(World, () =>
                    {
                        if (!tornado.IsValid()) { return; }
                        var donut = FireTornadoEntity.DonutMech(tornado);
                        attacks.Add(donut);
                    }, SecondDonutDelay);
                    attacks.Add(action1);
                }
                break;
            case DRAINAGE_ACTION_ID:
                if (!tornado.IsValid()) { return; }
                tornado.Destruct();
                break;
        }
    }
}
