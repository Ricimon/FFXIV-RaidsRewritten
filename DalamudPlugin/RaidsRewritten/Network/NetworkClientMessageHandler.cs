using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsyncAwaitBestPractices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using SocketIOClient;
using ZLinq;

namespace RaidsRewritten.Network;

public class NetworkClientMessageHandler(
    Lazy<NetworkClient> networkClient,
    DalamudServices dalamud,
    VfxSpawn vfxSpawn,
    Lazy<EcsContainer> ecsContainer,
    CommonQueries commonQueries,
    ILogger logger)
{
    private World World => ecsContainer.Value.World;

    public void OnMessage(SocketIOResponse response)
    {
        Message message;
        try
        {
            message = response.GetValue<Message>();
        }
        catch (JsonException jsonEx)
        {
            logger.Error("Failed to read server JSON data. You may need to update the plugin. Error:\n{0}", jsonEx.ToString());
            return;
        }
        catch (Exception e)
        {
            logger.Error(e.ToString());
            return;
        }

        switch (message.action)
        {
            case Message.Action.PlayVfx:
                if (message.playVfx != null) { PlayVfx(message.playVfx.Value); }
                break;
            case Message.Action.ApplyCondition:
                if (message.applyCondition != null) { ApplyCondition(message.applyCondition.Value); }
                break;
            case Message.Action.UpdatePartyStatus:
                if (message.updatePartyStatus != null) { UpdatePartyStatus(message.updatePartyStatus.Value); }
                break;
        }
    }

    private void PlayVfx(Message.PlayVfxPayload payload)
    {
        if (!Regex.IsMatch(payload.vfxPath, @"^vfx\/[\w\/]*\w+\.avfx$"))
        {
            logger.Error($"{payload.vfxPath} is not a valid VFX to play.");
            return;
        }

        dalamud.Framework.Run(() =>
        {
            var targetCharas = dalamud.ObjectTable.PlayerObjects.AsValueEnumerable().Where(pl =>
            {
                if (pl == null) { return false; }
                BattleChara bc;
                unsafe
                {
                    var bcA = (BattleChara*)pl.Address;
                    if (bcA == null) { return false; }
                    bc = *bcA;
                }
                return payload.targets.Contains(bc.ContentId);
            });

            foreach (var target in targetCharas)
            {
                vfxSpawn.SpawnActorVfx(payload.vfxPath, target, target);
            }
        }).SafeFireAndForget();
    }

    private void ApplyCondition(Message.ApplyConditionPayload payload)
    {
        switch (payload.condition)
        {
            case Message.ApplyConditionPayload.Condition.Stun:
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Stun.ApplyToTarget(e, payload.duration);
                });
                break;
        }
    }

    private void UpdatePartyStatus(Message.UpdatePartyStatusPayload payload)
    {
        networkClient.Value.ConnectedPlayersInParty = payload.connectedPlayersInParty;
    }
}
