using AsyncAwaitBestPractices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using RaidsRewritten.Network;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class JWaveSpreads : Mechanic
{
    private const uint JWaveActionId = 18551;

    private int jWaveCount = 0;

    public override void Reset()
    {
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
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = "JWave_" + jWaveCount,
                mechanicId = (uint)NetworkMechanic.TeaJWaveSpread,
                worldPositionX = bruteJustice.Position.X,
                worldPositionY = bruteJustice.Position.Y,
                worldPositionZ = bruteJustice.Position.Z,
                rotation = bruteJustice.Rotation,
            }
        }).SafeFireAndForget();
    }
}
