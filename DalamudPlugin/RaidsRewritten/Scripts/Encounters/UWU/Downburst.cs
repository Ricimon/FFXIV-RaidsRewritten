using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != UwuData.Garuda.Downburst) { return; }

        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        // Only applies punishment to tanks
        if (player.GetRole() != CombatRole.Tank) { return; }

        var playerPos = player.Position.ToVector2();

        // Count all (non-dead) tanks within soak range of the local player, including themselves
        int tanksInRange = 0;
        foreach (var member in Dalamud.ObjectTable.PlayerObjects)
        {
            if (member.IsDead) { continue; }
            if (member.GetRole() != CombatRole.Tank) { continue; }
            if (Vector2.Distance(member.Position.ToVector2(), playerPos) <= SoakRadius)
            {
                tanksInRange++;
            }
        }

        // If two or more tanks are soaking together, no punishment
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

#if DEBUG
    public override void DebugSimulate()
    {
        // Simulate solo-tank Downburst punishment (no other tank present)
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

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
#endif
}
