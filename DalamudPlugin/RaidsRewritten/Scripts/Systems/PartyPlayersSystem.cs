using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
        otherPlayersQuery.Dispose();
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

                foreach(var partyMember in dalamud.PartyList)
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

                foreach(var partyMember in dalamud.PartyList)
                {
                    if (partyMember?.GameObject == null || !partyMember.GameObject.IsValid())
                    {
                        continue;
                    }

                    var id = partyMember.GameObject.GameObjectId;
                    if (id == dalamud.ObjectTable.LocalPlayer.GameObjectId)
                    {
                        // Is local player
                        continue;
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
                        if (partyMember.GameObject is IPlayerCharacter pc)
                        {
                            Player.Create(it.World(), false, pc);
                            logger.Info("Created a Player entity with id {0}, name {1}", id, pc.GetPlayerFullName()!);
                        }
                    }
                }
            });
    }
}
