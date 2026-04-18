using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UWU;

public class GreatWhirlwindStacks : Mechanic
{
    private const float StackRadius = 6f;
    private const float StunDuration = 5f;
    private const float HeavyDuration = 10f;
    private const int RequiredStackSize = 4;
    private const float SnapshotDelay = 0.5f;
    private const float MarkerDuration = 10f;

    // Stack marker VFX shown above the local player
    private const string StackMarkerVfx = "vfx/lockon/eff/com_share2i.avfx";

    private readonly List<Vector3> pendingWhirlwindPositions = [];
    private readonly List<Entity> attacks = [];
    private Interop.Structs.Vfx.ActorVfx? activeStackMarker;

    public override void Reset()
    {
        pendingWhirlwindPositions.Clear();
        RemoveStackMarker();
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnStartingCast(Action action, IBattleChara source)
    {
        if (action.RowId != UwuData.Garuda.GreatWhirlwind) { return; }

        // Show stack marker on the local player when the cast begins
        ShowStackMarker();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != UwuData.Garuda.GreatWhirlwind) { return; }

        pendingWhirlwindPositions.Add(set.Position);

        // Wait for both instances to arrive, then snapshot
        if (pendingWhirlwindPositions.Count >= 2)
        {
            var positions = new List<Vector3>(pendingWhirlwindPositions);
            pendingWhirlwindPositions.Clear();

            var da = DelayedAction.Create(World, () =>
            {
                RemoveStackMarker();
                SnapshotStacks(positions);
            }, SnapshotDelay);
            attacks.Add(da);
        }
    }

    private void ShowStackMarker()
    {
        if (activeStackMarker != null) { return; }

        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        activeStackMarker = VfxSpawn.SpawnActorVfx(StackMarkerVfx, player, player);

        // Auto-remove marker after a timeout in case the action effect never fires
        var cleanup = DelayedAction.Create(World, RemoveStackMarker, MarkerDuration);
        attacks.Add(cleanup);
    }

    private void RemoveStackMarker()
    {
        if (activeStackMarker != null)
        {
            activeStackMarker.Remove();
            activeStackMarker = null;
        }
    }

#if DEBUG
    public override void DebugSimulate()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }

        // Show the stack marker
        ShowStackMarker();

        // Simulate two whirlwind positions near the player
        var playerPos = player.Position;
        var pos1 = playerPos + new Vector3(5f, 0, 0);
        var pos2 = playerPos + new Vector3(-5f, 0, 0);

        // Simulate the cast time before the action effect lands
        var castTime = 2.0f;

        var da = DelayedAction.Create(World, () =>
        {
            RemoveStackMarker();
            SnapshotStacks([pos1, pos2]);
        }, castTime + SnapshotDelay);
        attacks.Add(da);
    }
#endif

    private void SnapshotStacks(List<Vector3> whirlwindPositions)
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        // Determine which whirlwind the local player is closest to
        var playerPos = player.Position.ToVector2();
        int closestIndex = -1;
        float closestDist = float.MaxValue;

        for (int i = 0; i < whirlwindPositions.Count; i++)
        {
            var dist = Vector2.Distance(playerPos, whirlwindPositions[i].ToVector2());
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }

        if (closestIndex < 0 || closestDist > StackRadius) { return; }

        // Count how many players are stacked at the same whirlwind as the local player
        var stackCenter = whirlwindPositions[closestIndex].ToVector2();
        int playersInStack = 0;

        foreach (var partyMember in Dalamud.ObjectTable.PlayerObjects)
        {
            if (partyMember.IsDead) { continue; }
            var memberPos = partyMember.Position.ToVector2();
            if (Vector2.Distance(memberPos, stackCenter) <= StackRadius)
            {
                playersInStack++;
            }
        }

        if (playersInStack == RequiredStackSize) { return; }

        // Wrong number of players - apply penalty
        if (player.HasTranscendance())
        {
            VfxSpawn.PlayInvulnerabilityEffect(player);
            return;
        }

        CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
        {
            if (playersInStack < RequiredStackSize)
            {
                Stun.ApplyToTarget(e, StunDuration);
            }
            else
            {
                Heavy.ApplyToTarget(e, HeavyDuration);
            }
        });
    }
}
