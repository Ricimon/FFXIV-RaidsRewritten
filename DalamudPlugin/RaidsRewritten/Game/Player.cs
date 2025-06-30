using System;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Game;

public class Player(DalamudServices dalamud, PlayerManager playerManager, ILogger logger) : ISystem
{
    public record struct Component(object _);

    private readonly DalamudServices dalamud = dalamud;
    private readonly PlayerManager playerManager = playerManager;
    private readonly ILogger logger = logger;

    public static Entity Create(World world)
    {
        return world.Entity().Add<Component>();
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                try
                {
                    var playerEntity = it.Entity(i);

                    var player = this.dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead)
                    {
                        playerEntity.Each<Condition>(hc => hc.Mut(ref it).Destruct());
                        return;
                    }

                    var conditionCount = it.World().Query<Component, Condition>().Count();
                    if (conditionCount == 0)
                    {
                        this.logger.Info("Player has no conditions");
                    }

                    // Handle each condition
                    if (playerEntity.Has<KnockedBack.Component>())
                    {
                        var knockback = playerEntity.Get<KnockedBack.Component>();
                        this.logger.Info("Player has knockback, direction {0}, time left {1}", knockback.KnockbackDirection, knockback.TimeRemaining);

                        this.playerManager.OverrideMovement = true;
                        this.playerManager.OverrideMovementDirection = knockback.KnockbackDirection;
                    }
                    else
                    {
                        this.playerManager.OverrideMovement = false;
                    }
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
