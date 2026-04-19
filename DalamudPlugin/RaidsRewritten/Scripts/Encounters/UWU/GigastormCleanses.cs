using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UWU;

/// <summary>
/// Gigastorm / Cleanses mechanic.
///
/// While the mechanic is active (started by Eye of the Storm):
///   - Players INSIDE a bubble accumulate Light Resistance Down stacks.
///   - Players OUTSIDE all bubbles accumulate Dark Resistance Down stacks.
///   - The opposing stack type decays by 1 each tick.
///
/// Reaching <see cref="MaxStacks"/> of either type stuns the player.
/// Feather Rain clears the stun (stacks remain for ongoing tension).
/// </summary>
public class GigastormCleanses : Mechanic
{
    // Custom condition IDs — distinct from all vanilla status IDs
    private const int LightExposureId = 0xFE02;
    private const int DarkExposureId = 0xFE03;

    private const int MaxStacks = 5;
    private const float StunDuration = 30f;
    private const float StackConditionDuration = 999f;
    private const float BubbleRadius = 10f;

    // Wind-Scoured stacks 1-5: icons 219261-219265
    // Tempest Bound stacks 1-5: icons 219281-219285
    private static readonly int[] LightStatusIcons = [0, 219261, 219262, 219263, 219264, 219265];
    private static readonly int[] DarkStatusIcons  = [0, 219281, 219282, 219283, 219284, 219285];

    private readonly List<Vector3> bubblePositions = [];
    private int lightStacks = 0;
    private int darkStacks = 0;
    private Entity lightCondition = default;
    private Entity darkCondition = default;
    private bool mechanicActive = false;

    // Debug tick state
    private bool debugActive = false;
    private float debugTimer = 0f;
    private const float DebugTickInterval = 2f;
    private readonly List<Entity> debugSpawnedEntities = [];

    public override void Reset()
    {
        lightStacks = 0;
        darkStacks = 0;
        bubblePositions.Clear();
        mechanicActive = false;
        debugActive = false;
        debugTimer = 0f;
        foreach (var e in debugSpawnedEntities) { if (e.IsValid()) e.Destruct(); }
        debugSpawnedEntities.Clear();
        DestroyCondition(ref lightCondition);
        DestroyCondition(ref darkCondition);
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe || a3 == DirectorUpdateCategory.Recommence)
            Reset();
    }

    public override void OnCombatEnd() => Reset();

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (!debugActive) { return; }
        debugTimer += (float)framework.UpdateDelta.TotalSeconds;
        if (debugTimer < DebugTickInterval) { return; }
        debugTimer = 0f;
        CheckPositionAndApplyStacks();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        var rowId = set.Action.Value.RowId;

        // Eye of the Storm: define the safe bubble at the cast position and start the mechanic.
        if (rowId == UwuData.Garuda.EyeOfTheStorm)
        {
            bubblePositions.Clear();
            bubblePositions.Add(set.Position);
            mechanicActive = true;
        }

        // Friction: per-tick position check and stack application.
        if (mechanicActive && rowId == UwuData.Garuda.Friction)
        {
            CheckPositionAndApplyStacks();
        }

        // Feather Rain: cleanse the stun (stacks persist).
        if (rowId == UwuData.Garuda.FeatherRain)
        {
            ClearStun();
        }
    }

    private bool IsInAnyBubble(Vector3 pos)
    {
        var pos2 = pos.ToVector2();
        foreach (var center in bubblePositions)
        {
            if (Vector2.Distance(pos2, center.ToVector2()) <= BubbleRadius)
                return true;
        }
        return false;
    }

    private void CheckPositionAndApplyStacks()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead || bubblePositions.Count == 0) { return; }

        bool inside = IsInAnyBubble(player.Position);

        CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
        {
            if (inside)
            {
                // Inside bubble → gain Light, decay Dark
                IncrementStack(e, isLight: true);
                DecrementStack(isLight: false);
            }
            else
            {
                // Outside bubble → gain Dark, decay Light
                IncrementStack(e, isLight: false);
                DecrementStack(isLight: true);
            }
        });
    }

    private void IncrementStack(Entity playerEntity, bool isLight)
    {
        if (isLight) lightStacks++; else darkStacks++;
        int count = isLight ? lightStacks : darkStacks;
        int conditionId = isLight ? LightExposureId : DarkExposureId;
        string name = isLight ? "Wind-Scoured" : "Tempest Bound";
        string desc = isLight ? "Relentless winds have scoured away your resistance." : "Caught outside the barrier, the tempest bears down on you with full force.";
        int icon = isLight ? LightStatusIcons[count] : DarkStatusIcons[count];

        ref Entity condRef = ref (isLight ? ref lightCondition : ref darkCondition);

        var cond = Condition.ApplyToTarget(playerEntity, name, StackConditionDuration, conditionId, false, true);
        cond.Set(new Condition.Status(icon, name, desc))
            .Add<Condition.StatusEnfeeblement>();
        condRef = cond;

        if (count < MaxStacks) { return; }

        // Hit 5 stacks — stun the player and reset this stack type.
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player != null && player.HasTranscendance())
            this.VfxSpawn.PlayInvulnerabilityEffect(player);
        else
            Stun.ApplyToTarget(playerEntity, StunDuration);

        if (isLight) { lightStacks = 0; DestroyCondition(ref lightCondition); }
        else { darkStacks = 0; DestroyCondition(ref darkCondition); }
    }

    private void DecrementStack(bool isLight)
    {
        if (isLight)
        {
            if (lightStacks <= 0) { return; }
            lightStacks = Math.Max(0, lightStacks - 1);
            if (lightStacks == 0) { DestroyCondition(ref lightCondition); return; }
            if (lightCondition.IsValid())
                lightCondition.Set(new Condition.Status(LightStatusIcons[lightStacks], "Wind-Scoured", "Relentless winds have scoured away your resistance."));
        }
        else
        {
            if (darkStacks <= 0) { return; }
            darkStacks = Math.Max(0, darkStacks - 1);
            if (darkStacks == 0) { DestroyCondition(ref darkCondition); return; }
            if (darkCondition.IsValid())
                darkCondition.Set(new Condition.Status(DarkStatusIcons[darkStacks], "Tempest Bound", "Caught outside the barrier, the tempest bears down on you with full force."));
        }
    }

    private void ClearStun()
    {
        CommonQueries.LocalPlayerQuery.Each((Entity playerEntity, ref Player.Component _) =>
        {
            var world = playerEntity.CsWorld();
            using var q = world.QueryBuilder<Condition.Component, Condition.Id>()
                .With(Ecs.ChildOf, playerEntity).Build();
            q.Each((Entity cond, ref Condition.Component _, ref Condition.Id id) =>
            {
                if (id.Value == Stun.Id)
                    cond.Destruct();
            });
        });
    }

    private static void DestroyCondition(ref Entity e)
    {
        if (e.IsValid()) e.Destruct();
        e = default;
    }

#if DEBUG
    public override void DebugSimulate()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        Reset();

        // Place the bubble 15y north of the player — walk in to gain Light stacks, stay out for Dark.
        var bubbleCenter = player.Position + new Vector3(0f, 0f, -15f);
        bubblePositions.Add(bubbleCenter);
        mechanicActive = true;
        debugActive = true;

        // Spawn Garuda at the bubble center (the "Eye of the Storm")
        if (EntityManager.TryCreateEntity<Garuda>(out var garuda))
        {
            garuda.Set(new Position(bubbleCenter));
            garuda.Set(new Rotation(0f));
            debugSpawnedEntities.Add(garuda);
        }

        // Show a circle omen marking the bubble boundary
        var bubbleOmen = World.Entity()
            .Set(new StaticVfx("vfx/omen/eff/general_1bf.avfx"))
            .Set(new Position(bubbleCenter))
            .Set(new Rotation(0f))
            .Set(new Scale(new Vector3(BubbleRadius)))
            .Add<Omen>();
        debugSpawnedEntities.Add(bubbleOmen);
    }
#endif
}
