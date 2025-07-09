using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Game;

public class Player(DalamudServices dalamud, PlayerManager playerManager, Configuration configuration, ILogger logger) : ISystem
{
    public record struct Component(bool IsLocalPlayer);

    private readonly DalamudServices dalamud = dalamud;
    private readonly PlayerManager playerManager = playerManager;
    private readonly Configuration configuration = configuration;
    private readonly ILogger logger = logger;

    public static Entity Create(World world, bool isLocalPlayer)
    {
        return world.Entity().Set(new Component(isLocalPlayer));
    }

    public static Query<Component> Query(World world)
    {
        return world.Query<Component>();
    }

    public void Register(World world)
    {
        var knockbackQuery = world.QueryBuilder<Condition.Component, KnockedBack.Component>().Cached().Build();
        var boundQuery = world.QueryBuilder<Condition.Component, Bound.Component>().Cached().Build();

        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                try
                {
                    if (!component.IsLocalPlayer) { return; }

                    var playerEntity = it.Entity(i);

                    var player = this.dalamud.ClientState.LocalPlayer;
                    if (this.configuration.EverythingDisabled || player == null || player.IsDead)
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
                    if (knockbackQuery.IsTrue())
                    {
                        var knockbackEntity = knockbackQuery.First();
                        var condition = knockbackEntity.Get<Condition.Component>();
                        var knockback = knockbackEntity.Get<KnockedBack.Component>();
                        //this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, condition.TimeRemaining);

                        this.playerManager.OverrideMovement = true;
                        this.playerManager.OverrideMovementDirection = knockback.KnockbackDirection;
                    }
                    else
                    {
                        if (boundQuery.IsTrue())
                        {
                            this.playerManager.OverrideMovement = true;
                            this.playerManager.OverrideMovementDirection = Vector3.Zero;
                        }
                        else
                        {
                            this.playerManager.OverrideMovement = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
