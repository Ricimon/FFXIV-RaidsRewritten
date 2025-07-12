using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Conditions;

public sealed class KnockedBack(DalamudServices dalamud, EcsContainer ecsContainer, ILogger logger) : IDalamudHook
{
    public record struct Component(Vector3 KnockbackDirection);

    private static readonly List<uint> KnockbackNullificationStatuses = [
        160,    // Surecast
        1209,   // Arm's Length
        1984,   // Arm's Length (another version)
        2663,   // Inner Strength
        2345,   // Lost Manawall (from Bozja)
        4235,   // Rage (from Occult Crescent)
        75,     // Tempered Will
        712,    // Tempered Will (another version)
        //2702,   // Radiant Aegis (testing)
        ];

    private readonly World world = ecsContainer.World;

    public static void ApplyToPlayer(Entity playerEntity, Vector3 knockbackDirection, float duration, bool canResist)
    {
        var apply = true;
        playerEntity.Scope(() =>
        {
            // Don't apply if player is bound
            using var q = playerEntity.CsWorld().Query<Bound.Component>();
            if (q.IsTrue())
            {
                apply = false;
                return;
            }
        });

        if (apply && canResist)
        {
            var player = playerEntity.TryGet<Player.Component>(out var pc) ? pc.PlayerCharacter : null;
            if (player != null)
            {
                foreach (var status in player.StatusList)
                {
                    if (KnockbackNullificationStatuses.Contains(status.StatusId))
                    {
                        apply = false;
                        return;
                    }
                }
            }
        }

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
            var localPlayer = dalamud.ClientState.LocalPlayer;
            if (localPlayer == null) { return; }
            foreach (var targetEffects in set.TargetEffects)
            {
                if (targetEffects.TargetID == localPlayer.GameObjectId &&
                    (targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback1, out _) ||
                    targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback2, out _)))
                {
                    // Remove any fake knockback conditions if a real knockback occurs
                    using var q = Player.Query(this.world);
                    q.Each((Entity e, ref Player.Component _) =>
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
            logger.Error(e.ToStringFull());
        }
    }
}
