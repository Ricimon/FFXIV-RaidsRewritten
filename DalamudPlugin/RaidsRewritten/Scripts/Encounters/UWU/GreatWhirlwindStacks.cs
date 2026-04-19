using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
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

public class GreatWhirlwindStacks : Mechanic
{
    private const float StackRadius = 6f;
    private const int RequiredStackSize = 4;
    private const float SnapshotDelay = 0.5f;
    private const float FireResDownDuration = 15f;
    private const float HysteriaDuration = 10f;
    private const float MarchDuration = 15f;
    private const int FireResDownId = 0xFE01;
    // DSR DarkdragonDive tower VFX family (Omen 120, 335, 336, 337)
    private static readonly string[] TowerOmenVfxByCount =
    [
        "vfx/omen/eff/m0119_trap_01t.avfx",  // [0] unused
        "vfx/omen/eff/m0119_trap_01t.avfx",  // [1] 1-person bait tower
        "vfx/omen/eff/general_trap_o2x.avfx", // [2] 2-person tower
        "vfx/omen/eff/general_trap_o3x.avfx", // [3] 3-person tower
        "vfx/omen/eff/general_trap_o4x.avfx", // [4] 4-person tower
    ];
    private const float WhirlwindRadius = 12f;

    public int RngSeed { get; set; }
    public bool RandomTowerOffset { get; set; } = true;
    private Random random = new();

    private readonly List<Vector3> pendingWhirlwindPositions = [];
    private readonly List<Entity> attacks = [];
    private readonly List<Entity> activeTowerOmens = [];
    private readonly List<Entity> activeWhirlwindCircles = [];

    public override void Reset()
    {
        random = new Random(RngSeed);
        pendingWhirlwindPositions.Clear();
        RemoveTowerOmens();
        RemoveWhirlwindCircles();
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != UwuData.Garuda.GreatWhirlwind) { return; }

        pendingWhirlwindPositions.Add(set.Position);

        if (pendingWhirlwindPositions.Count >= 2)
        {
            pendingWhirlwindPositions.Clear();
            var player = Dalamud.ObjectTable.LocalPlayer;
            if (player == null) { return; }
            SpawnTowersInZone(player.Position, previewDelay: 0f);
        }
    }

    private void SpawnTowersInZone(Vector3 zoneCenter, float previewDelay)
    {
        // Spawn one whirlwind zone visual centered on the player (persists until Reset)
        var whirlwindCircle = World.Entity()
            .Set(new StaticVfx("vfx/omen/eff/tatumaki0m.avfx"))
            .Set(new Position(zoneCenter))
            .Set(new Rotation(0f))
            .Set(new Scale(new Vector3(WhirlwindRadius, WhirlwindRadius, WhirlwindRadius)))
            .Add<Omen>();
        activeWhirlwindCircles.Add(whirlwindCircle);

        // Determine soaker split FIRST
        int r1 = random.Next(1, 4); // 1, 2, or 3  →  splits: 1+3, 2+2, 3+1
        var requiredCounts = new List<int>(2) { r1, RequiredStackSize - r1 };

        // Place 2 towers at random offsets within WhirlwindRadius of the zone center
        var towerPositions = new List<Vector3>(2);
        for (int i = 0; i < 2; i++)
        {
            Vector3 pos;
            if (RandomTowerOffset)
            {
                var angle = random.NextSingle() * MathF.PI * 2f;
                var dist = (0.3f + random.NextSingle() * 0.4f) * WhirlwindRadius;
                pos = new Vector3(
                    zoneCenter.X + MathF.Sin(angle) * dist,
                    zoneCenter.Y,
                    zoneCenter.Z + MathF.Cos(angle) * dist);
            }
            else
            {
                // Fixed positions within zone: +X and -X at half radius
                var offset = (i == 0 ? 1f : -1f) * WhirlwindRadius * 0.5f;
                pos = new Vector3(zoneCenter.X + offset, zoneCenter.Y, zoneCenter.Z);
            }
            towerPositions.Add(pos);
        }

        for (int i = 0; i < towerPositions.Count; i++)
        {
            var count = Math.Clamp(requiredCounts[i], 1, TowerOmenVfxByCount.Length - 1);
            var vfx = TowerOmenVfxByCount[count];
            var towerOmen = World.Entity()
                .Set(new StaticVfx(vfx))
                .Set(new Position(towerPositions[i]))
                .Set(new Rotation(0f))
                .Set(new Scale(new Vector3(3f, 5f, 3f)))
                .Add<Omen>();
            activeTowerOmens.Add(towerOmen);
        }

        var da = DelayedAction.Create(World, () =>
        {
            SnapshotStacks(towerPositions, requiredCounts);
        }, previewDelay + SnapshotDelay);
        attacks.Add(da);
    }

    private void RemoveTowerOmens()
    {
        foreach (var omen in activeTowerOmens)
        {
            if (omen.IsValid()) omen.Destruct();
        }
        activeTowerOmens.Clear();
    }

    private void RemoveWhirlwindCircles()
    {
        foreach (var circle in activeWhirlwindCircles)
        {
            if (circle.IsValid()) circle.Destruct();
        }
        activeWhirlwindCircles.Clear();
    }

#if DEBUG
    public override void DebugSimulate()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }

        SpawnTowersInZone(player.Position, previewDelay: 2.0f);
    }
#endif

    private void SnapshotStacks(List<Vector3> towerPositions, List<int> requiredCounts)
    {
        RemoveTowerOmens();

        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        var playerPos2 = player.Position.ToVector2();

        // Count soakers per tower; track which tower the local player is standing in
        var soakerCounts = new int[towerPositions.Count];
        int playerTowerIndex = -1;
        for (int i = 0; i < towerPositions.Count; i++)
        {
            var towerPos2 = towerPositions[i].ToVector2();

            if (Vector2.Distance(playerPos2, towerPos2) <= StackRadius)
                playerTowerIndex = i;

            foreach (var member in Dalamud.ObjectTable.PlayerObjects)
            {
                if (member.IsDead) { continue; }
                if (Vector2.Distance(member.Position.ToVector2(), towerPos2) <= StackRadius)
                    soakerCounts[i]++;
            }
        }

        // Success: player is in a tower that met its required soaker count
        bool playerTowerSatisfied = playerTowerIndex >= 0
            && soakerCounts[playerTowerIndex] >= requiredCounts[playerTowerIndex];

        if (playerTowerSatisfied)
        {
            CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
            {
                bool alreadyDebuffed = false;
                using var q = e.CsWorld().QueryBuilder<Condition.Component, Condition.Id>()
                    .With(Ecs.ChildOf, e).Build();
                q.Each((ref Condition.Component _, ref Condition.Id id) =>
                {
                    if (id.Value == FireResDownId) alreadyDebuffed = true;
                });

                if (alreadyDebuffed)
                    Hysteria.ApplyToTarget(e, HysteriaDuration, 0.5f);
                else
                    ApplyFireResistanceDown(e, FireResDownDuration);
            });
            return;
        }

        // Failure: player not in a tower, or tower under-soaked
        if (player.HasTranscendance())
        {
            this.VfxSpawn.PlayInvulnerabilityEffect(player);
            return;
        }

        CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
        {
            var forward = new Vector3(MathF.Sin(player.Rotation), 0, MathF.Cos(player.Rotation));
            ForcedMarch.ApplyMarchToTarget(e, forward, MarchDuration);
        });
    }

    private static void ApplyFireResistanceDown(Entity target, float duration)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, "Fire Resistance Down", duration, FireResDownId, false, false);
            condition.Set(new Condition.Status(215595, "Fire Resistance Down", "Fire resistance is reduced.")).Add<Condition.StatusEnfeeblement>();
        }, 0, true).ChildOf(target);
    }
}
