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

    public static void ApplyToTarget(Entity playerEntity, Vector3 knockbackDirection, float duration, bool canResist)
    {
        if (!playerEntity.CsWorld().IsDeferred())
        {
            Logger?.Warn("Knockback application must be done in a Deferred context.");
            return;
        }

        var apply = true;
        playerEntity.Children(child =>
        {
            // Don't apply if player is bound
            if (child.Has<Bind.Component>())
            {
                apply = false;
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
        playerEntity.Children(child =>
        {
            // This operation requires deferral
            if (child.Has<Component>()) { child.Destruct(); }
        });

        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Knocked Back", duration))
            .Set(new Component(knockbackDirection))
            .ChildOf(playerEntity);
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
            foreach (var targetEffects in set.TargetEffects)
            {
                if (targetEffects.TargetID == localPlayer.GameObjectId &&
                    (targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback1, out _) ||
                    targetEffects.GetSpecificTypeEffect(ActionEffectType.Knockback2, out _)))
                {
                    // Remove any fake knockback conditions if a real knockback occurs
                    this.world.DeferBegin();
                    this.commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                    {
                        e.Children(child =>
                        {
                            // This operation requires deferral
                            if (child.Has<Component>()) { child.Destruct(); }
                        });
                    });
                    this.world.DeferEnd();
                    return;
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
