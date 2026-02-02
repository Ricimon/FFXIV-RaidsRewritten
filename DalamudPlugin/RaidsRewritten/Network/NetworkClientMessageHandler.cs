using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsyncAwaitBestPractices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
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
    Configuration configuration,
    ILogger logger)
{
    private World World => ecsContainer.Value.World;

    public void OnMessage(SocketIOResponse response)
    {
        if (configuration.EverythingDisabled)
        {
            return;
        }

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
            case Message.Action.ApplyCondition:
                if (message.applyCondition != null) { ApplyCondition(message.applyCondition.Value); }
                break;
            case Message.Action.UpdatePartyStatus:
                if (message.updatePartyStatus != null) { UpdatePartyStatus(message.updatePartyStatus.Value); }
                break;
            case Message.Action.PlayStaticVfx:
                if (message.playStaticVfx != null) { PlayStaticVfx(message.playStaticVfx.Value); }
                break;
            case Message.Action.PlayActorVfxOnTarget:
                if (message.playActorVfxOnTarget != null) { PlayActorVfxOnTarget(message.playActorVfxOnTarget.Value); }
                break;
            case Message.Action.PlayActorVfxOnPosition:
                if (message.playActorVfxOnPosition != null) { PlayActorVfxOnPosition(message.playActorVfxOnPosition.Value); }
                break;
            case Message.Action.StopVfx:
                if (message.stopVfx != null) { StopVfx(message.stopVfx.Value); }
                break;
        }
    }

    private bool CheckIsValidVfxPath(string path)
    {
        if (!Regex.IsMatch(path, @"(^vfx|^bg)\/[\w\/]*\w+\.avfx$"))
        {
            logger.Error($"{path} is not a valid VFX to play.");
            return false;
        }
        return true;
    }

    private void ApplyCondition(Message.ApplyConditionPayload payload)
    {
        // Despite these operations not destructing any entities, any kind of operations on the EcsWorld have to be forwarded
        // to the main thread here or else Flecs could cause the game to crash.
        dalamud.Framework.Run(() =>
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
        }).SafeFireAndForget();
    }

    private void UpdatePartyStatus(Message.UpdatePartyStatusPayload payload)
    {
        networkClient.Value.ConnectedPlayersInParty = payload.connectedPlayersInParty;
    }

    private void PlayStaticVfx(Message.PlayStaticVfxPayload payload)
    {
        if (!CheckIsValidVfxPath(payload.vfxPath)) { return; }

        dalamud.Framework.Run(() =>
        {
            var e = World.Entity()
                .Set(new StaticVfx(payload.vfxPath))
                .Set(new VfxId(payload.id))
                .Set(new Position(new(payload.worldPositionX, payload.worldPositionY, payload.worldPositionZ)))
                .Set(new Rotation(payload.rotation))
                .Set(new Scale())
                .Add<Attack>();

            if (payload.isOmen)
            {
                e.Add<Omen>();
            }
        }).SafeFireAndForget();
    }

    private void PlayActorVfxOnTarget(Message.PlayActorVfxOnTargetPayload payload)
    {
        if (!CheckIsValidVfxPath(payload.vfxPath)) { return; }

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
                return payload.contentIdTargets.Contains(bc.ContentId);
            });

            foreach (var target in targetCharas)
            {
                vfxSpawn.SpawnActorVfx(payload.vfxPath, target, target);
            }

            // TODO: Use customIdTargets
        }).SafeFireAndForget();
    }

    private void PlayActorVfxOnPosition(Message.PlayActorVfxOnPositionPayload payload)
    {
        if (!CheckIsValidVfxPath(payload.vfxPath)) { return; }

        dalamud.Framework.Run(() =>
        {
            FakeActor.Create(World)
                .Set(new ActorVfx(payload.vfxPath))
                .Set(new Position(new(payload.worldPositionX, payload.worldPositionY, payload.worldPositionZ)))
                .Set(new Rotation(payload.rotation));
        }).SafeFireAndForget();
    }

    private void StopVfx(Message.StopVfxPayload payload)
    {
        dalamud.Framework.Run(() =>
        {
            World.Defer(() =>
            {
                World.Query<VfxId>().Each((Iter it, int i, ref VfxId vfxId) =>
                {
                    if (vfxId.Value == payload.id)
                    {
                        it.Entity(i).Destruct();
                    }
                });
            });
        }).SafeFireAndForget();
    }
}
