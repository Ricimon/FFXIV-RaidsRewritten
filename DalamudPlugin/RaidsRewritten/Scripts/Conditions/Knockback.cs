﻿using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Conditions;

public sealed class Knockback : IDalamudHook
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

    private static ILogger? Logger;

    private readonly DalamudServices dalamud;
    private readonly World world;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    public static void ApplyToTarget(Entity target, Vector3 knockbackDirection, float duration, bool canResist)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var apply = true;
            target.Children(child =>
            {
                // Don't apply if player is bound
                if (child.Has<Bind.Component>())
                {
                    apply = false;
                }
            });

            if (apply && canResist)
            {
                var player = target.TryGet<Player.Component>(out var pc) ? pc.PlayerCharacter : null;
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
            target.Children(child =>
            {
                // This operation requires deferral
                if (child.Has<Component>())
                {
                    child.CsWorld().DeferResume();
                    child.Destruct();
                    child.CsWorld().DeferSuspend();
                }
            });

            it.World().Entity()
                .Set(new Condition.Component("Knocked Back", duration, DateTime.UtcNow))
                .Set(new Component(knockbackDirection))
                .ChildOf(target);
        }, 0, true).ChildOf(target);
    }

    public Knockback(DalamudServices dalamud, EcsContainer ecsContainer, CommonQueries commonQueries, ILogger logger)
    {
        Logger = logger;

        this.dalamud = dalamud;
        this.world = ecsContainer.World;
        this.commonQueries = commonQueries;
        this.logger = logger;
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
        // Do not Destruct entities in an undeferred query or else this will cause a crash.
        try
        {
            var localPlayer = this.dalamud.ClientState.LocalPlayer;
            if (localPlayer == null) { return; }

            // Remove any fake knockbacks if the player performs some movement action
            if (set.Source != null && set.Source.GameObjectId == localPlayer.GameObjectId)
            {
                if (set.Action.HasValue && set.Action.Value.AffectsPosition)
                {
                    float delay = 0;
                    // All actions have some amount of application delay, but I don't know where to find that
                    // info, so these values are found through testing
                    if (set.Action.Value.RowId == 2262) // Shukuchi
                    {
                        delay = 0.267f;
                    }
                    RemoveKnockback(delay);
                }
                return;
            }

            foreach (var targetEffects in set.TargetEffects)
            {
                if (targetEffects.TargetID == localPlayer.GameObjectId)
                {
                    EffectEntry effect;
                    if (targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback1, out var kb1))
                    {
                        effect = kb1;
                    }
                    else if (targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback2, out var kb2))
                    {
                        effect = kb2;
                    }
                    else
                    {
                        continue;
                    }

                    float delay = 0;
                    // All actions have some amount of application delay, but I don't know where to find that
                    // info, so these values are found through testing
                    if (set.Action?.RowId == 7571) // Rescue
                    {
                        delay = 0.433f;
                    }

                    // Remove any fake knockback conditions if a real knockback occurs
                    RemoveKnockback(delay);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            logger.Error(e.ToStringFull());
        }
    }

    private void RemoveKnockback(float delay)
    {
        if (delay > 0)
        {
            DelayedAction.Create(this.world, () =>
            {
                this.commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                {
                    e.DestructChildEntity<Component>();
                });
            }, delay);
        }
        else
        {
            this.world.DeferBegin();
            this.commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
            {
                // This operation requires deferral
                e.DestructChildEntity<Component>();
            });
            this.world.DeferEnd();
        }
    }
}
