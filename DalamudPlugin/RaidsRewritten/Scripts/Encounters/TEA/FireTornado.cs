using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class FireTornado : Mechanic
{
    private const uint LIQUID_RAGE_DATA_ID = 0x2C49;

    private readonly List<Entity> attacks = [];
    private readonly IReadOnlyList<Vector3> tornadoPositions =
        [new(85, 0, 100),
        new(115, 0, 100),
        new(100, 0, 85),
        new(100, 0, 115)];

    private HashSet<Vector3> availableTornadoPositions = [];

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        availableTornadoPositions = [.. tornadoPositions];
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

        availableTornadoPositions.Remove(newObject.Position);

        if (availableTornadoPositions.Count == 1)
        {
            var position = availableTornadoPositions.ElementAt(0);
            availableTornadoPositions.Clear();

            var tornado = FireTornadoEntity.CreateEntity(World)
                .Set(new Position(position));

            attacks.Add(tornado);
        }
    }
}
