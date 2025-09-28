using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class PermanentViceOfApathyTest : Mechanic
{
    private const uint ViceOfApathyDataId = 0x1EAE20;
    private const float SpawnDelay = 1.0f;

    private readonly List<Entity> entities = [];

    public override void Reset()
    {
        foreach (var e in entities)
        {
            e.Destruct();
        }
        entities.Clear();
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
        if (newObject.DataId != ViceOfApathyDataId) { return; }

        void CreateTwister()
        {
            var player = this.Dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.EntityManager.TryCreateEntity<Twister>(out var twister))
                {
                    twister.Set(new Position(newObject.Position));
                    twister.Set(new Rotation(newObject.Rotation));
                    entities.Add(twister);
                }
            }
        }

        var delayedAction = DelayedAction.Create(this.World, CreateTwister, SpawnDelay);
        entities.Add(delayedAction);
    }
}
