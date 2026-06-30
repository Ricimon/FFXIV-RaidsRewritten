using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public sealed class PartyPlayersSystem(DalamudServices dalamud, Configuration configuration, ILogger logger) : ISystem, IDisposable
{
    private Query<Player.Component> otherPlayersQuery;

    public void Dispose()
    {
        otherPlayersQuery.SafeDispose();
    }

    public void Register(World world)
    {
        otherPlayersQuery = world.QueryBuilder<Player.Component>().Without<Player.LocalPlayer>().Cached().Build();

        // Cleanup player entities no longer in the party
        world.System<Player.Component>()
            .Without<Player.LocalPlayer>()
            .Each((Iter it, int i, ref Player.Component player) =>
            {
                if (configuration.EverythingDisabled)
                {
                    it.Entity(i).Destruct();
                    logger.Info("Destructed a Player entity");
                    return;
                }

                if (player.PlayerCharacter == null || !player.PlayerCharacter.IsValid())
                {
                    it.Entity(i).Destruct();
                    logger.Info("Destructed a Player entity");
                    return;
                }

                foreach (var partyMember in dalamud.PartyList)
                {
                    if (partyMember?.GameObject == null || !partyMember.GameObject.IsValid())
                    {
                        continue;
                    }

                    if (player.PlayerCharacter.GameObjectId == partyMember.GameObject.GameObjectId)
                    {
                        return;
                    }
                }

                if (dalamud.Condition[ConditionFlag.DutyRecorderPlayback])
                {
                    foreach (var p in dalamud.ObjectTable.PlayerObjects)
                    {
                        if (player.PlayerCharacter.GameObjectId == p.GameObjectId)
                        {
                            return;
                        }
                    }
                }

                logger.Info("Destructed a Player entity with id {0}, name {1}", player.PlayerCharacter.GameObjectId, player.PlayerCharacter.GetPlayerFullName()!);
                it.Entity(i).Destruct();
            });

        // Create Player entities for party members
        world.System()
            .Each((Iter it, int _) =>
            {
                if (configuration.EverythingDisabled)
                {
                    return;
                }

                if (!dalamud.PlayerState.IsLoaded ||
                    dalamud.ObjectTable.LocalPlayer == null || !dalamud.ObjectTable.LocalPlayer.IsValid())
                {
                    return;
                }

                foreach (var partyMember in dalamud.PartyList)
                {
                    var gameObject = partyMember?.GameObject;
                    if (ShouldCreatePlayerEntity(gameObject) && gameObject is IPlayerCharacter pc)
                    {
                        Player.Create(it.World(), false, pc, partyMember!.ContentId);
                        logger.Info("Created a Player entity with id {0}, name {1}", gameObject.GameObjectId, pc.GetPlayerFullName()!);
                    }
                }

                // The party list is empty during duty recorder playback
                if (dalamud.Condition[ConditionFlag.DutyRecorderPlayback])
                {
                    foreach (var player in dalamud.ObjectTable.GetPlayers())
                    {
                        if (ShouldCreatePlayerEntity(player))
                        {
                            BattleChara bc;
                            unsafe
                            {
                                var bcp = (BattleChara*)player.Address;
                                if (bcp == null) { continue; }
                                bc = *bcp;
                            }
                            Player.Create(it.World(), false, player, bc.ContentId);
                            logger.Info("Created a Player entity with id {0}, name {1}", player.GameObjectId, player.GetPlayerFullName()!);
                        }
                    }
                }
            });
    }

    private bool ShouldCreatePlayerEntity(IGameObject? player)
    {
        if (player == null || !player.IsValid() || player.ObjectKind != ObjectKind.Pc)
        {
            return false;
        }

        // A strange behavior where sometimes no-name Pc GameObjects will flicker in and out of existence in Duty Recorder
        if (player.Name.Payloads.Count == 0)
        {
            return false;
        }

        var id = player.GameObjectId;
        if (id == dalamud.ObjectTable.LocalPlayer?.GameObjectId)
        {
            // Is local player
            return false;
        }

        var playerEntity = otherPlayersQuery.Find((ref p) =>
        {
            if (p.PlayerCharacter == null || !p.PlayerCharacter.IsValid())
            {
                return false;
            }
            return p.PlayerCharacter.GameObjectId == id;
        });

        if (!playerEntity.IsValid())
        {
            return true;
        }

        return false;
    }
}
