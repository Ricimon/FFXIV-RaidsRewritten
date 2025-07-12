using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Game;

public class Player(DalamudServices dalamud, PlayerManager playerManager, Configuration configuration, ILogger logger) : ISystem
{
    public record struct Component(bool IsLocalPlayer, IPlayerCharacter? PlayerCharacter);

    public static Entity Create(World world, bool isLocalPlayer)
    {
        return world.Entity().Set(new Component(isLocalPlayer, null));
    }

    public static Query<Component> Query(World world)
    {
        return world.Query<Component>();
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                try
                {
                    if (!component.IsLocalPlayer) { return; }

                    var playerEntity = it.Entity(i);

                    var player = dalamud.ClientState.LocalPlayer;
                    component.PlayerCharacter = dalamud.ClientState.LocalPlayer;
                    if (configuration.EverythingDisabled || player == null || player.IsDead)
                    {
                        playerEntity.Children(c =>
                        {
                            if (c.Has<Condition.Component>())
                            {
                                c.Mut(ref it).Destruct();
                            }
                        });
                    }

                    it.World().SetScope(playerEntity);
                    //if (!it.World().Query<Condition>().IsTrue())
                    //{
                    //    this.logger.Info("Player has no conditions");
                    //}

                    // Handle each condition
                    using var knockbackQuery = world.Query<Condition.Component, KnockedBack.Component>();
                    if (knockbackQuery.IsTrue())
                    {
                        var knockbackEntity = knockbackQuery.First();
                        var condition = knockbackEntity.Get<Condition.Component>();
                        var knockback = knockbackEntity.Get<KnockedBack.Component>();
                        //this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, condition.TimeRemaining);

                        playerManager.OverrideMovement = true;
                        playerManager.OverrideMovementDirection = knockback.KnockbackDirection;
                    }
                    else
                    {
                        using var boundQuery = world.Query<Condition.Component, Bound.Component>();
                        if (boundQuery.IsTrue())
                        {
                            playerManager.OverrideMovement = true;
                            playerManager.OverrideMovementDirection = Vector3.Zero;
                        }
                        else
                        {
                            playerManager.OverrideMovement = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }
}
