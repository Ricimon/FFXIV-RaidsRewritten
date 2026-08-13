using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaPark : Mechanic
{
    private const uint LiquidRageDataId = 0x2C49;
    private const uint FluidSwingActionId = 18864;
    private const uint AetherCompassActionId = 26988;

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
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private HashSet<Vector3> availableTornadoPositions = [];
    private int fluidSwingsPerformed = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        availableTornadoPositions.Clear();
        fluidSwingsPerformed = 0;
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
        if (newObject.BaseId != LiquidRageDataId) { return; }
        if (availableTornadoPositions.Count == 1) { return; }

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
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == FluidSwingActionId)
        {
            fluidSwingsPerformed++;
            if (fluidSwingsPerformed == 3 && availableTornadoPositions.Count == 1)
            {
                var openTornadoPosition = availableTornadoPositions.Single();
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = NetworkMechanic.TeaSpawnShanoa.ToString(),
                        mechanicId = (int)NetworkMechanic.TeaSpawnShanoa,
                        worldPositionX = openTornadoPosition.X,
                        worldPositionY = openTornadoPosition.Y,
                        worldPositionZ = openTornadoPosition.Z,
                        rotation = default(float),
                    }
                }).SafeFireAndForget();
            }
        }

        else if (set.Action.Value.RowId == AetherCompassActionId)
        {

        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        if (payload.mechanicCommandId == (int)NetworkMechanicCommand.TeaShowShanoa)
        {
            if (EntityManager.TryCreateEntity<Shanoa>(out var shanoa))
            {
                shanoa
                    .Set(new Position(new(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default)))
                    .Set(new Rotation(payload.rotation ?? default))
                    .Set(new ChatBubble("Meow!♪"));
                attacks.Add(shanoa);
            }
        }
    }
}
