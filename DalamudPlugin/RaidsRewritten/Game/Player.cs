using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Flecs.NET.Core;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class Player(DalamudServices dalamud, PlayerManager playerManager, Configuration configuration, ILogger logger) : ISystem, IDisposable
{
    public record struct Component(IPlayerCharacter? PlayerCharacter);
    public struct LocalPlayer;

    private Query<Condition.Component, Knockback.Component> knockbackQuery;
    private Query<Condition.Component, Bind.Component> bindQuery;
    private Query<Condition.Component, Stun.Component> stunQuery;
    private Query<Condition.Component, Paralysis.Component> paralysisQuery;
    private Query<Condition.Component, Heavy.Component> heavyQuery;
    private Query<Condition.Component, Pacify.Component> pacifyQuery;
    private Query<Condition.Component, Sleep.Component> sleepQuery;
    private Query<Condition.Component, Hysteria.Component> hysteriaQuery;
    private Query<Condition.Component> overheatQuery;
    private Query<Condition.Component> deepfreezeQuery;

    public static Entity Create(World world, bool isLocalPlayer)
    {
        var entity = world.Entity().Set(new Component(null));
        if (isLocalPlayer)
        {
            entity.Add<LocalPlayer>();
        }
        return entity;
    }

    public void Dispose()
    {
        this.knockbackQuery.Dispose();
        this.bindQuery.Dispose();
        this.stunQuery.Dispose();
        this.paralysisQuery.Dispose();
        this.heavyQuery.Dispose();
        this.pacifyQuery.Dispose();
        this.sleepQuery.Dispose();
        this.hysteriaQuery.Dispose();
        this.overheatQuery.Dispose();
        this.deepfreezeQuery.Dispose();
    }

    public static Query<Component> QueryForLocalPlayer(World world)
    {
        return world.QueryBuilder<Component>().With<LocalPlayer>().Cached().Build();
    }

    public void Register(World world)
    {
        this.knockbackQuery = world.QueryBuilder<Condition.Component, Knockback.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.bindQuery = world.QueryBuilder<Condition.Component, Bind.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.stunQuery = world.QueryBuilder<Condition.Component, Stun.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.paralysisQuery = world.QueryBuilder<Condition.Component, Paralysis.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.heavyQuery = world.QueryBuilder<Condition.Component, Heavy.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.pacifyQuery = world.QueryBuilder<Condition.Component, Pacify.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.sleepQuery = world.QueryBuilder<Condition.Component, Sleep.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.hysteriaQuery = world.QueryBuilder<Condition.Component, Hysteria.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.overheatQuery = world.QueryBuilder<Condition.Component>()
            .With<Overheat.Component>()
            .With<LocalPlayer>().Up().Cached().Build();
        this.deepfreezeQuery = world.QueryBuilder<Condition.Component>()
            .With<Deepfreeze.Component>()
            .With<LocalPlayer>().Up().Cached().Build();

        world.System<Component>().With<LocalPlayer>()
            .Each((Iter it, int i, ref Component component) =>
            {
                try
                {
                    var playerEntity = it.Entity(i);

                    var player = dalamud.ObjectTable.LocalPlayer;
                    component.PlayerCharacter = dalamud.ObjectTable.LocalPlayer;
                    if (configuration.EverythingDisabled || player == null || player.IsDead)
                    {
                        playerEntity.Children(c =>
                        {
                            var destroy = c.Has<Condition.Component>();
                            // Ignore specific conditions under normal circumstances
                            if (!configuration.EverythingDisabled && player != null)
                            {
                                destroy &= !c.Has<Condition.IgnoreOnDeath>();
                            }
                            if (destroy)
                            {
                                c.Mut(ref it).Destruct();
                            }
                        });

                        DisableAllOverrides();
                        return;
                    }

#if DEBUG
                    if (configuration.PunishmentImmunity)
                    {
                        DisableAllOverrides();
                        return;
                    }
#endif

                    // Handle each condition
                    bool stun = false;
                    bool disableAllActions = false;

                    Entity knockbackEntity = this.knockbackQuery.First();

                    Entity bindEntity = this.bindQuery.First();

                    Entity stunEntity = this.stunQuery.First();
                    stun |= stunEntity.IsValid();

                    Entity sleepEntity = this.sleepQuery.First();
                    stun |= sleepEntity.IsValid();

                    Entity deepfreezeEntity = this.deepfreezeQuery.First();
                    stun |= deepfreezeEntity.IsValid();

                    Entity hysteriaEntity = this.hysteriaQuery.First();
                    disableAllActions |= hysteriaEntity.IsValid();

                    this.paralysisQuery.Each((Entity e, ref Condition.Component _, ref Paralysis.Component paralysis) =>
                    {
                        stun |= paralysis.StunActive;
                    });

                    Entity heavyEntity = this.heavyQuery.First();

                    Entity pacifyEntity = this.pacifyQuery.First();

                    Entity overheatEntity = this.overheatQuery.First();

                    disableAllActions |= stun;

                    // Condition effects
                    if (knockbackEntity.IsValid())
                    {
                        var condition = knockbackEntity.Get<Condition.Component>();
                        var knockback = knockbackEntity.Get<Knockback.Component>();
                        //this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, condition.TimeRemaining);

                        playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.ForceMovementWorldDirection;
                        playerManager.OverrideMovementWorldDirection = knockback.KnockbackDirection;
                        playerManager.ForceWalk = PlayerMovementOverride.ForcedWalkState.Run;
                    }
                    else
                    {
                        playerManager.OverrideMovementWorldDirection = Vector3.Zero;

                        if (bindEntity.IsValid() || stun)
                        {
                            playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.ForceMovementWorldDirection;
                        }
                        else
                        {
                            if (hysteriaEntity.IsValid())
                            {
                                var hysteria = hysteriaEntity.Get<Hysteria.Component>();

                                playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.ForceMovementWorldDirection;
                                playerManager.OverrideMovementWorldDirection = hysteria.MoveDirection;
                            }
                            else if (overheatEntity.IsValid())
                            {
                                playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.ForceMovementCameraDirection;
                                playerManager.OverrideMovementCameraDirection = Vector2.UnitY;
                            }
                            else
                            {
                                playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.None;
                            }

                            if (heavyEntity.IsValid())
                            {
                                playerManager.ForceWalk = PlayerMovementOverride.ForcedWalkState.Walk;
                            }
                            else
                            {
                                playerManager.ForceWalk = PlayerMovementOverride.ForcedWalkState.None;
                            }
                        }
                    }

                    // Action override
                    playerManager.DisableAllActions = disableAllActions;
                    playerManager.DisableDamagingActions = pacifyEntity.IsValid();
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }

    private void DisableAllOverrides()
    {
        playerManager.OverrideMovement = PlayerMovementOverride.OverrideMovementState.None;
        playerManager.ForceWalk = PlayerMovementOverride.ForcedWalkState.None;
        playerManager.DisableAllActions = false;
        playerManager.DisableDamagingActions = false;
    }
}
