using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Game;

public sealed class Player(DalamudServices dalamud, PlayerManager playerManager, Configuration configuration, ILogger logger) : ISystem, IDisposable
{
    public record struct Component(bool IsLocalPlayer, IPlayerCharacter? PlayerCharacter);

    private Query<Component, Condition.Component, Knockback.Component> knockbackQuery;
    private Query<Component, Condition.Component, Bind.Component> bindQuery;
    private Query<Component, Condition.Component, Stun.Component> stunQuery;
    private Query<Component, Condition.Component, Paralysis.Component> paralysisQuery;
    private Query<Component, Condition.Component, Heavy.Component> heavyQuery;

    public static Entity Create(World world, bool isLocalPlayer)
    {
        return world.Entity().Set(new Component(isLocalPlayer, null));
    }

    public void Dispose()
    {
        this.knockbackQuery.Dispose();
        this.bindQuery.Dispose();
        this.stunQuery.Dispose();
        this.paralysisQuery.Dispose();
        this.heavyQuery.Dispose();
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
        this.stunQuery = world.QueryBuilder<Component, Condition.Component, Stun.Component>()
            .TermAt(0).Up().Cached().Build();
        this.paralysisQuery = world.QueryBuilder<Component, Condition.Component, Paralysis.Component>()
            .TermAt(0).Up().Cached().Build();
        this.heavyQuery = world.QueryBuilder<Component, Condition.Component, Heavy.Component>()
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
                    bool stun = false;
                    Entity knockbackEntity = GetFirstConditionEntityOnLocalPlayer(this.knockbackQuery);
                    Entity bindEntity = GetFirstConditionEntityOnLocalPlayer(this.bindQuery);
                    Entity stunEntity = GetFirstConditionEntityOnLocalPlayer(this.stunQuery);
                    stun |= stunEntity.IsValid();
                    this.paralysisQuery.Each((Entity e, ref Component pc, ref Condition.Component _, ref Paralysis.Component paralysis) =>
                    {
                        if (!pc.IsLocalPlayer) { return; }
                        stun |= paralysis.StunActive;
                    });
                    Entity slowEntity = GetFirstConditionEntityOnLocalPlayer(this.heavyQuery);

                    // Movement override
                    if (knockbackEntity.IsValid())
                    {
                        var condition = knockbackEntity.Get<Condition.Component>();
                        var knockback = knockbackEntity.Get<Knockback.Component>();
                        //this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, condition.TimeRemaining);

                        playerManager.OverrideMovement = true;
                        playerManager.OverrideMovementDirection = knockback.KnockbackDirection;
                        playerManager.ForceWalk = Interop.PlayerMovementOverride.ForcedWalkState.Run;
                    }
                    else
                    {
                        playerManager.OverrideMovementDirection = Vector3.Zero;

                        if (bindEntity.IsValid() || stun)
                        {
                            playerManager.OverrideMovement = true;
                        }
                        else if (slowEntity.IsValid())
                        {
                            playerManager.OverrideMovement = false;
                            playerManager.ForceWalk = Interop.PlayerMovementOverride.ForcedWalkState.Walk;
                        }
                        else
                        {
                            playerManager.OverrideMovement = false;
                            playerManager.ForceWalk = Interop.PlayerMovementOverride.ForcedWalkState.None;
                        }
                    }

                    // Action override
                    playerManager.DisableAllActions = stun;
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
