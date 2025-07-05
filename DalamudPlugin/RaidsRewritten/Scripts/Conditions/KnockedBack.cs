using System;
using System.Numerics;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public sealed class KnockedBack(DalamudServices dalamud, EcsContainer ecsContainer, ILogger logger) : IDalamudHook
{
    public record struct Component(Vector3 KnockbackDirection);

    private readonly DalamudServices dalamud = dalamud;
    private readonly World world = ecsContainer.World;
    private readonly ILogger logger = logger;

    public static void ApplyToPlayer(Entity playerEntity, Vector3 knockbackDirection, float duration)
    {
        // Don't apply if player is bound
        var apply = true;
        playerEntity.Scope(() =>
        {
            if (playerEntity.CsWorld().Query<Bound.Component>().IsTrue())
            {
                apply = false;
            }
        });

        if (!apply) { return; }

        // Remove existing knockback conditions
        playerEntity.Scope(() =>
        {
            playerEntity.CsWorld().DeleteWith<Component>();
        });

        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Knocked Back", duration))
            .Set(new Component(knockbackDirection))
            .ChildOf(playerEntity);
    }

    public void HookToDalamud()
    {
        ActionEffect.ActionEffectEvent += OnActionEffectEvent;
    }

    public void Dispose()
    {
        ActionEffect.ActionEffectEvent -= OnActionEffectEvent;
    }

    private void OnActionEffectEvent(ActionEffectSet set)
    {
        try
        {
            var localPlayer = this.dalamud.ClientState.LocalPlayer;
            if (localPlayer == null) { return; }
            foreach (var targetEffects in set.TargetEffects)
            {
                if (targetEffects.TargetID == localPlayer.GameObjectId &&
                    (targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback1, out _) ||
                    targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback2, out _)))
                {
                    // Remove any fake knockback conditions if a real knockback occurs
                    Player.Query(this.world).Each((Entity e, ref Player.Component _) =>
                    {
                        e.Scope(() =>
                        {
                            this.world.DeleteWith<Component>();
                        });
                    });
                    return;

                    // Do not Destruct entities outside of a system or else this will cause a crash.
                    // Instead, use scopes and World.DeleteWith()
                }
            }

            // TODO: Remove any fake knockback conditions if any movement abilities occur (Thunderclap, getting Rescued, etc)
        }
        catch (Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }
    }
}
