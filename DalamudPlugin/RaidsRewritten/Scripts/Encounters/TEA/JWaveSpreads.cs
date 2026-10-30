using System.Collections.Generic;
using AsyncAwaitBestPractices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Network;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class JWaveSpreads : Mechanic
{
    private const uint JWaveActionId = 18551;
    // Action effect fires on snapshot, damage lands a bit later
    private const float SpreadDelay = 0.6f;

    private readonly List<Entity> delayedSends = [];

    private int jWaveCount = 0;

    public override void Reset()
    {
        foreach (var delayedSend in delayedSends)
        {
            delayedSend.Destruct();
        }
        delayedSends.Clear();

        jWaveCount = 0;
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != JWaveActionId) { return; }

        var bruteJustice = set.Source;
        if (bruteJustice == null) { return; }

        jWaveCount++;
        var requestId = "JWave_" + jWaveCount;
        var position = bruteJustice.Position;
        var rotation = bruteJustice.Rotation;

        var delayedSend = DelayedAction.Create(World, () =>
        {
            NetworkClient.SendAsync(new Message
            {
                action = Message.Action.StartMechanic,
                startMechanic = new Message.StartMechanicPayload
                {
                    requestId = requestId,
                    mechanicId = (uint)NetworkMechanic.TeaJWaveSpread,
                    worldPositionX = position.X,
                    worldPositionY = position.Y,
                    worldPositionZ = position.Z,
                    rotation = rotation,
                }
            }).SafeFireAndForget();
        }, SpreadDelay);
        delayedSends.Add(delayedSend);
    }
}
