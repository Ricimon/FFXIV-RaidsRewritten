using System;
using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class WormholeSuperJump : Mechanic
{
    // Keep in sync with the server
    private enum Phase
    {
        Spawn = 0,
        SuperJump = 1,
        Teleport = 2,
        ApocalypticRay = 3,
        Leave = 4,
    }

    private const uint AlexanderPrimeBaseId = 0x2C53;
    // Alex becomes targetable when he first shows up, after Inception and after Wormhole
    private const int TargetableAfterWormhole = 3;

    private const int BruteJusticeModelId = 1357;
    private const ushort IdleTimeline = 34;
    // Same warp out and in as the end of P2 and timestop
    private const ushort WarpOutTimeline = 7737;
    private const ushort WarpInTimeline = 7747;
    // Super Jump's cast, mon_sp_c
    private const ushort CastingStartTimeline = 134;
    private const ushort CastingTimeline = 135;
    private const ushort SuperJumpTimeline = 3204;
    private const ushort ApocalypticRayTimeline = 3205;

    // Both come from the Wormhole Formation wormholes
    private const string WormholeVfx = "bg/ex1/02_dra_d2/common/vfx/eff/b0982zige1_o.avfx";
    private const string TeleportVfx = "bg/ex1/02_dra_d2/common/vfx/eff/b0982zige2_o.avfx";
    // Same warp in/out as VoidGate
    private const string WarpInVfx = "vfx/monster/c0101/eff/c0101wpinc0c.avfx";
    private const string WarpOutVfx = "vfx/monster/c0101/eff/c0101wpouc0c.avfx";
    private const string WormholeExplosionVfx = "vfx/monster/d1025/eff/d1025_sp12_bunsan_zentai_t0s.avfx";

    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    // Both from BJ's Super Jump timeline (mon_sp004), which doesn't play them on our model
    private const string TakeOffVfx = "vfx/common/eff/cmma_shoot1c.avfx";
    private const string LandingVfx = "vfx/monster/m0300/eff/m0300sp04c0t.avfx";
    // From BJ's Apocalyptic Ray timeline (mon_sp005)
    private const string ApocalypticRayVfx = "vfx/monster/m0300/eff/m0300sp05c0t.avfx";

    // Same as the real one in the fight
    private const float BruteJusticeScale = 0.45f;
    // Second stage size, after one soak. Keep in sync with wormhole_radius on the server
    private const float WormholeScale = 0.75f;
    // BJ is a lot bigger than a player
    private const float WarpVfxScale = 4.0f;
    // Warp in plus the idle has to fit before the server's cast_time runs out
    private const float WarpInDuration = 2.0f;
    private const float IdleBeforeCast = 0.3f;
    // He crouches before leaving the ground
    private const float JumpWindup = 0.4f;
    // Windup plus this has to match the 1s landing in the server's jump_time
    private const float JumpDuration = 0.6f;
    private const float JumpHeight = 10.0f;
    // Keep under the server's turn_time so he's done turning before the ray
    private const float TurnDuration = 0.75f;
    // Keep under the server's teleport_time so he's moved before the ray
    private const float TeleportDelay = 1.5f;
    // Hides him being moved, has to fit in TeleportDelay
    private const float WarpFadeDuration = 1.0f;
    private const float FadeOutDuration = 1.5f;

    private const float ApocalypticRayLength = 40.0f;
    private const int ApocalypticRayDegrees = 90;
    private const float ApocalypticRayTickInterval = 1.0f;
    // Safe spot is as wide as Sacrament
    private const float DynamoSafeRadius = 16.0f;
    // The server snapshots it, this just fills the omen
    private const float DynamoDuration = 3.0f;
    private const float DamageDownDuration = 60.0f;

    private readonly List<Entity> attacks = [];

    private Entity bruteJustice;
    private Entity castingVfx;
    private Entity wormhole;
    private Entity exitWormhole;
    private Entity dynamo;
    private float bruteJusticeAlpha = 1.0f;
    private float bruteJusticeTargetAlpha = 1.0f;
    private bool jumping = false;
    private Vector3 jumpStart;
    private Vector3 jumpEnd;
    private float jumpElapsed;
    private bool turning = false;
    private float turnStart;
    private float turnAngle;
    private float turnElapsed;
    private int timesTargetable = 0;
    private int timesExecuted = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        bruteJustice = default;
        castingVfx = default;
        wormhole = default;
        exitWormhole = default;
        dynamo = default;
        bruteJusticeAlpha = 1.0f;
        bruteJusticeTargetAlpha = 1.0f;
        jumping = false;
        turning = false;
        timesTargetable = 0;
        timesExecuted = 0;
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

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (!bruteJustice.IsValid()) { return; }

        var delta = (float)framework.UpdateDelta.TotalSeconds;
        UpdateJump(delta);
        UpdateTurn(delta);
        UpdateFade(delta);
    }

    private void UpdateJump(float delta)
    {
        if (!jumping) { return; }

        jumpElapsed += delta;
        var t = MathF.Min(jumpElapsed / JumpDuration, 1.0f);
        var position = Vector3.Lerp(jumpStart, jumpEnd, t);
        position.Y += 4.0f * JumpHeight * t * (1.0f - t);
        bruteJustice.Set(new Position(position));

        if (t < 1.0f) { return; }

        jumping = false;
        World.Entity()
            .Set(new ActorVfx(LandingVfx))
            .ChildOf(bruteJustice);
        CreateDynamo();
    }

    private void CreateDynamo()
    {
        var center = new Vector3(100.0f, 0.0f, 100.0f);
        foreach (var obj in Dalamud.ObjectTable)
        {
            if (obj.BaseId == AlexanderPrimeBaseId)
            {
                center = obj.Position;
                break;
            }
        }

        dynamo.SafeDestruct();
        dynamo = OneThirdDonutOmen.CreateEntity(World)
            .Set(new Position(center))
            .Set(new Scale(DynamoSafeRadius / 0.3f * Vector3.One))
            .Set(new OmenDuration(DynamoDuration, false));
        attacks.Add(dynamo);
    }

    // Goes off at the same time as the wormhole
    private void SnapshotDynamo()
    {
        if (!dynamo.IsValid()) { return; }

        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player != null && !player.IsDead && OneThirdDonutOmen.IsInOmen(dynamo, player.Position))
        {
            if (player.HasTranscendance())
            {
                VfxSpawn.PlayInvulnerabilityEffect(player);
            }
            else
            {
                CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                {
                    DamageDown.ApplyToTarget(e, DamageDownDuration);
                });
            }
        }
        dynamo.SafeDestruct();
    }

    private void TurnTo(float rotation)
    {
        if (!bruteJustice.TryGet(out Rotation current)) { return; }

        turnStart = current.Value;
        // Shortest way round
        turnAngle = MathF.IEEERemainder(rotation - current.Value, 2.0f * MathF.PI);
        turnElapsed = 0.0f;
        turning = true;
    }

    private void UpdateTurn(float delta)
    {
        if (!turning) { return; }

        turnElapsed += delta;
        var t = MathF.Min(turnElapsed / TurnDuration, 1.0f);
        bruteJustice.Set(new Rotation(turnStart + turnAngle * t));
        turning = t < 1.0f;
    }

    private void UpdateFade(float delta)
    {
        if (bruteJusticeAlpha == bruteJusticeTargetAlpha) { return; }

        var step = delta / WarpFadeDuration;
        bruteJusticeAlpha = bruteJusticeAlpha < bruteJusticeTargetAlpha
            ? MathF.Min(bruteJusticeAlpha + step, bruteJusticeTargetAlpha)
            : MathF.Max(bruteJusticeAlpha - step, bruteJusticeTargetAlpha);
        bruteJustice.Set(new Alpha(bruteJusticeAlpha));
    }

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        // Command 54, p1 1 is become targetable
        if (source.BaseId == AlexanderPrimeBaseId && command == 54 && p1 == 1)
        {
            timesTargetable++;
            if (timesTargetable == TargetableAfterWormhole)
            {
                StartMechanic();
            }
        }
    }

    public override void DebugSimulate()
    {
        StartMechanic();
    }

    private void StartMechanic()
    {
        timesExecuted++;
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = "WormholeSuperJump_" + timesExecuted,
                mechanicId = (uint)NetworkMechanic.TeaWormholeSuperJump,
            }
        }).SafeFireAndForget();
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        if (payload.mechanicCommandId != (int)NetworkMechanicCommand.TeaUpdateWormholeSuperJump) { return; }
        if (string.IsNullOrEmpty(payload.extraData)) { return; }
        var arguments = payload.extraData.Split(',');
        if (arguments.Length < 7) { return; }
        if (!int.TryParse(arguments[0], out var phase)) { return; }
        if (!float.TryParse(arguments[1], out var wormholeX)) { return; }
        if (!float.TryParse(arguments[2], out var wormholeZ)) { return; }
        if (!float.TryParse(arguments[3], out var exitWormholeX)) { return; }
        if (!float.TryParse(arguments[4], out var exitWormholeZ)) { return; }
        if (!int.TryParse(arguments[5], out var usedWormhole)) { return; }
        if (!float.TryParse(arguments[6], out var apocalypticRayDuration)) { return; }

        var position = new Vector3(
            payload.worldPositionX ?? default,
            payload.worldPositionY ?? default,
            payload.worldPositionZ ?? default);
        var rotation = payload.rotation ?? default;

        switch ((Phase)phase)
        {
            case Phase.Spawn:
                Spawn(position, rotation,
                    new Vector3(wormholeX, position.Y, wormholeZ),
                    new Vector3(exitWormholeX, position.Y, exitWormholeZ));
                break;
            case Phase.SuperJump:
                SuperJump(position, rotation);
                break;
            case Phase.Teleport:
                SnapshotDynamo();
                if (usedWormhole == 1)
                {
                    Teleport(position, rotation);
                }
                else
                {
                    ExplodeWormhole();
                    TurnTo(rotation);
                }
                break;
            case Phase.ApocalypticRay:
                ApocalypticRay(position, rotation, apocalypticRayDuration);
                break;
            case Phase.Leave:
                Leave();
                break;
        }
    }

    private void Spawn(Vector3 position, float rotation, Vector3 wormholePosition, Vector3 exitWormholePosition)
    {
        bruteJustice.SafeDestruct();
        bruteJustice = World.Entity()
            .Set(new Model(BruteJusticeModelId))
            .Set(new Position(position))
            .Set(new Rotation(rotation))
            .Set(new UniformScale(BruteJusticeScale))
            .Set(new TimelineBase(IdleTimeline))
            .Set(new OneTimeModelTimeline(WarpInTimeline))
            .Add<Attack>();
        attacks.Add(bruteJustice);

        var cast = DelayedAction.Create(World, () =>
        {
            if (!bruteJustice.IsValid()) { return; }
            bruteJustice
                .Set(new TimelineBase(CastingTimeline))
                .Set(new OneTimeModelTimeline(CastingStartTimeline));
            castingVfx = World.Entity()
                .Set(new ActorVfx(CastingVfx))
                .ChildOf(bruteJustice);
        }, WarpInDuration + IdleBeforeCast);
        attacks.Add(cast);

        wormhole.SafeDestruct();
        wormhole = CreateWormhole(wormholePosition);
        attacks.Add(wormhole);

        // Can't be reached, just shows where BJ comes out if he takes the other one
        exitWormhole.SafeDestruct();
        exitWormhole = CreateWormhole(exitWormholePosition);
        attacks.Add(exitWormhole);
    }

    // Arena vfx, only shows up as a static vfx, not on an actor
    private Entity CreateWormhole(Vector3 position)
    {
        return World.Entity()
            .Set(new StaticVfx(WormholeVfx))
            .Set(new Position(position))
            .Set(new Rotation(0.0f))
            .Set(new Scale(WormholeScale * Vector3.One))
            .Add<Attack>();
    }

    private void SuperJump(Vector3 landingPosition, float rotation)
    {
        if (!bruteJustice.IsValid() || !bruteJustice.TryGet(out Position start)) { return; }

        castingVfx.SafeDestruct();
        bruteJustice
            .Set(new Rotation(rotation))
            .Set(new TimelineBase(IdleTimeline))
            .Set(new OneTimeModelTimeline(SuperJumpTimeline));

        // Flown in OnFrameworkUpdate once he's off the ground
        jumpStart = start.Value;
        jumpEnd = landingPosition;
        jumpElapsed = 0.0f;
        var takeOff = DelayedAction.Create(World, () =>
        {
            if (!bruteJustice.IsValid()) { return; }
            World.Entity()
                .Set(new ActorVfx(TakeOffVfx))
                .ChildOf(bruteJustice);
            jumping = true;
        }, JumpWindup);
        attacks.Add(takeOff);
    }

    private void Teleport(Vector3 position, float rotation)
    {
        if (!bruteJustice.IsValid() || !bruteJustice.TryGet(out Position entry)) { return; }

        // Light up both ends first so it's clear where he's going
        attacks.Add(CreateTeleportVfx(entry.Value));
        attacks.Add(CreateTeleportVfx(position));
        attacks.Add(CreateWarpVfx(WarpInVfx, entry.Value));
        bruteJusticeTargetAlpha = 0.0f;

        var move = DelayedAction.Create(World, () =>
        {
            if (!bruteJustice.IsValid()) { return; }
            bruteJustice
                .Set(new Position(position))
                .Set(new Rotation(rotation));
            attacks.Add(CreateWarpVfx(WarpOutVfx, position));
            bruteJusticeTargetAlpha = 1.0f;

            // The wormhole is used up
            wormhole.SafeDestruct();
        }, TeleportDelay);
        attacks.Add(move);
    }

    // Only plays on an actor and actor vfx ignore Scale, so it goes on a scaled up model like the
    // alternate model in the debug tab
    private Entity CreateWarpVfx(string path, Vector3 position)
    {
        return World.Entity()
            .Set(new Model(392))
            .Set(new Position(position))
            .Set(new Rotation(0.0f))
            .Set(new ActorVfx(path))
            .Set(new UniformScale(WarpVfxScale))
            .Set(new ModelHeight(WarpVfxScale))
            .Add<Attack>();
    }

    private Entity CreateTeleportVfx(Vector3 position)
    {
        return FakeActor.CreateInvalidModel(World)
            .Set(new Position(position))
            .Set(new ActorVfx(TeleportVfx));
    }

    private void ApocalypticRay(Vector3 position, float rotation, float duration)
    {
        if (!bruteJustice.IsValid()) { return; }

        bruteJustice
            .Set(new Position(position))
            .Set(new Rotation(rotation))
            // Otherwise he goes back to the last timeline when the ray's done
            .Set(new TimelineBase(IdleTimeline))
            .Set(new OneTimeModelTimeline(ApocalypticRayTimeline));
        // The timeline doesn't play its own vfx on our model
        World.Entity()
            .Set(new ActorVfx(ApocalypticRayVfx))
            .ChildOf(bruteJustice);

        // No omen for the ray, same as the real one in P2
        // Lingers, so tick it like the real thing
        for (var t = 0.0f; t < duration; t += ApocalypticRayTickInterval)
        {
            var tick = DelayedAction.Create(World, () =>
            {
                if (EntityManager.TryCreateEntity<Fan>(out var fan))
                {
                    fan
                        .Set(new Position(position))
                        .Set(new Rotation(rotation))
                        .Set(new Scale(ApocalypticRayLength * Vector3.One))
                        .Set(new Fan.Component(OnApocalypticRayHit, ApocalypticRayDegrees));
                }
            }, t);
            attacks.Add(tick);
        }
    }

    private void OnApocalypticRayHit(Entity e)
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead) { return; }
        if (player.HasTranscendance())
        {
            VfxSpawn.PlayInvulnerabilityEffect(player);
        }
        else
        {
            DamageDown.ApplyToTarget(e, DamageDownDuration);
        }
    }

    // Hysteria comes from the server
    private void ExplodeWormhole()
    {
        if (!wormhole.TryGet(out Position wormholePosition)) { return; }

        var explosion = FakeActor.CreateInvalidModel(World)
            .Set(new Position(wormholePosition.Value))
            .Set(new ActorVfx(WormholeExplosionVfx));
        attacks.Add(explosion);
        wormhole.SafeDestruct();
    }

    private void Leave()
    {
        if (bruteJustice.IsValid() && bruteJustice.TryGet(out Model model))
        {
            bruteJustice
                .Set(new OneTimeModelTimeline(WarpOutTimeline))
                .Set(new ModelFadeOut(model.ObjectIndex, FadeOutDuration, FadeOutDuration));
        }

        var cleanup = DelayedAction.Create(World, () =>
        {
            wormhole.SafeDestruct();
            exitWormhole.SafeDestruct();
        }, FadeOutDuration);
        attacks.Add(cleanup);
    }
}
