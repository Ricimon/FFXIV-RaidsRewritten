using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class Player(DalamudServices dalamud, PlayerManager playerManager, Configuration configuration, ILogger logger) : ISystem, IDisposable
{
    public record struct Component(bool IsLocalPlayer, IPlayerCharacter? PlayerCharacter);

    private Query<Component, Condition.Component, Knockback.Component> knockbackQuery;
    private Query<Component, Condition.Component, Bind.Component> bindQuery;

    public static Entity Create(World world, bool isLocalPlayer)
    {
        return world.Entity().Set(new Component(isLocalPlayer, null));
    }

    public void Dispose()
    {
        this.knockbackQuery.Dispose();
        this.bindQuery.Dispose();
    }

    public static Query<Component> Query(World world)
    {
        return world.Query<Component>();
    }

    public void Register(World world)
    {
        this.knockbackQuery = world.QueryBuilder<Component, Condition.Component, Knockback.Component>()
            .TermAt(0).Up().Cached().Build();
        this.bindQuery = world.QueryBuilder<Component, Condition.Component, Bind.Component>()
            .TermAt(0).Up().Cached().Build();

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

                    // Handle each condition
                    Entity knockbackEntity = GetFirstConditionEntityOnLocalPlayer(this.knockbackQuery);
                    Entity bindEntity = GetFirstConditionEntityOnLocalPlayer(this.bindQuery);

                    if (knockbackEntity.IsValid())
                    {
                        var condition = knockbackEntity.Get<Condition.Component>();
                        var knockback = knockbackEntity.Get<Knockback.Component>();
                        //this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, condition.TimeRemaining);

                        playerManager.OverrideMovement = true;
                        playerManager.OverrideMovementDirection = knockback.KnockbackDirection;
                    }
                    else
                    {
                        if (bindEntity.IsValid())
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

    private static Entity GetFirstConditionEntityOnLocalPlayer<T>(Query<Component, Condition.Component, T> query)
    {
        Entity entity = default;
        query.Each((Entity e, ref Component pc, ref Condition.Component _, ref T _) =>
        {
            if (!pc.IsLocalPlayer) { return; }
            if (entity.IsValid()) { return; }
            entity = e;
        });
        return entity;
    }
}
