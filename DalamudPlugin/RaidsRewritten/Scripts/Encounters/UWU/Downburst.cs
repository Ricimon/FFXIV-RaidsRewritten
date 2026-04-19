using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
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
/// Downburst soaking check: both tanks must stand together when Garuda's Downburst resolves.
/// If the local player is a tank and no other tank is within <see cref="SoakRadius"/> yards,
/// they are stunned for <see cref="StunDuration"/> seconds (until the next Feather Rain window).
/// </summary>
public class Downburst : Mechanic
{
    private const float SoakRadius = 5f;
    private const float StunDuration = 25f;

    private readonly List<Entity> spawnedEntities = [];

    public override void Reset()
    {
        CleanupSpawned();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe || a3 == DirectorUpdateCategory.Recommence)
            Reset();
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != UwuData.Garuda.Downburst) { return; }

        if (Dalamud.ObjectTable.LocalPlayer is not IBattleChara player || player.IsDead) { return; }

        if (player.GetRole() != CombatRole.Tank) { return; }

        DoTankCheck(player);
    }

    private void DoTankCheck(IBattleChara player)
    {
        var playerPos = player.Position.ToVector2();

        int tanksInRange = 0;
        foreach (var member in Dalamud.ObjectTable.PlayerObjects)
        {
            if (member.IsDead) { continue; }
            if (member.GetRole() != CombatRole.Tank) { continue; }
            if (Vector2.Distance(member.Position.ToVector2(), playerPos) <= SoakRadius)
                tanksInRange++;
        }

        if (tanksInRange >= 2) { return; }

        if (player.HasTranscendance())
        {
            this.VfxSpawn.PlayInvulnerabilityEffect(player);
            return;
        }

        CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
        {
            Stun.ApplyToTarget(e, StunDuration);
        });
    }

    private void CleanupSpawned()
    {
        foreach (var e in spawnedEntities)
        {
            if (e.IsValid()) e.Destruct();
        }
        spawnedEntities.Clear();
    }

#if DEBUG
    public override void DebugSimulate()
    {
        if (Dalamud.ObjectTable.LocalPlayer is not IBattleChara player || player.IsDead) { return; }

        CleanupSpawned();

        // Spawn Garuda 15y north of the player, facing toward her
        var garudaPos = player.Position + new Vector3(0f, 0f, -15f);
        if (EntityManager.TryCreateEntity<Garuda>(out var garuda))
        {
            garuda.Set(new Position(garudaPos));
            garuda.Set(new Rotation(MathF.PI)); // face south toward player
            spawnedEntities.Add(garuda);
        }

        // Show soaking circle omen at player's position
        var omen = World.Entity()
            .Set(new StaticVfx("vfx/omen/eff/general_1bf.avfx"))
            .Set(new Position(player.Position))
            .Set(new Rotation(0f))
            .Set(new Scale(new Vector3(SoakRadius)))
            .Add<Omen>();
        spawnedEntities.Add(omen);

        // After cast delay: resolve tank check and clean up
        var da = DelayedAction.Create(World, () =>
        {
            CleanupSpawned();
            DoTankCheck(player);
        }, 2.5f);
        spawnedEntities.Add(da);
    }
#endif
}

