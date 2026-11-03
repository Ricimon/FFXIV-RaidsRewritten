using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Network;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndAlexPrime : Mechanic
{
    public IShanoaPark? ShanoaPark { get; set; }

    private const uint SummonAlexanderActionId = 19029;
    private const uint JStormActionId = 18550;
    private const uint CruiseChaserBaseId = 0x2C4E;
    private const uint BruteJusticeBaseId = 0x2C4C;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
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

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    {
        if (action.RowId == SummonAlexanderActionId)
        {
            var position = arenaMiddle;
            position.Z = 121.4f;
            var rotation = MathUtilities.VectorToRotation(new(0, -1));
            NetworkClient.SendAsync(new Message
            {
                action = Message.Action.StartMechanic,
                startMechanic = new Message.StartMechanicPayload
                {
                    requestId = NetworkMechanic.TeaShowShanoa + "_SummonAlexander",
                    mechanicId = (uint)NetworkMechanic.TeaShowShanoa,
                    worldPositionX = position.X,
                    worldPositionY = position.Y,
                    worldPositionZ = position.Z,
                    rotation = rotation,
                }
            }).SafeFireAndForget();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == JStormActionId)
        {
            ShanoaPark?.AttackMarkers("jstorm");
        }
    }

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if (source.BaseId == CruiseChaserBaseId &&
            command == 537) // command 537 seems to be death at P3 enrage
        {
            ShanoaPark?.AttackMarkers("cruisechaserdefeat");
        }

        else if (source.BaseId == BruteJusticeBaseId &&
            command == 537) // command 537 seems to be death at P3 enrage
        {
            ShanoaPark?.AttackMarkers("brutejusticedefeat");
        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaShanoaAbsorbsMarker:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 2) { return; }
                    if (!byte.TryParse(arguments[1], out var totalMarkersAbsorbed)) { return; }
                    if (totalMarkersAbsorbed >= 8)
                    {
                        // do something cool
                    }
                }
                break;
        }
    }
}
