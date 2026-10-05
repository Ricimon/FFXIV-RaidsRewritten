using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class GearBeams : Mechanic
{
    public int RngSeed { get; set; }

    private const uint ALEXANDER_PRIME_BASE_ID = 0x2C53;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private int timesExecuted = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
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

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        // Command 54, p1 1 is become targetable
        // Command 54, p1 0 is become untargetable
        if (source.BaseId == ALEXANDER_PRIME_BASE_ID && command == 54 && p1 == 1)
        {
            StartMechanic(++timesExecuted);
        }
    }

    public override void DebugSimulate()
    {
        StartMechanic(++timesExecuted);
    }

    private void StartMechanic(int index)
    {
        var totalGears = 0;
        switch (index)
        {
            case 1: totalGears = 2; break;
            case 2: totalGears = 3; break;
            default: return;
        }

        var seed = RngSeed;
        unchecked
        {
            seed += 0x4EA5 + index;
        }
        var random = new Random(seed);

        var gear1Distance = 11.5f;
        var gear2Distance = 20.0f;
        var gear3Distance = 23.7f;
        var gearRadius = 5.0f;
        var gearPlacementRotation = random.Next(8) * 0.25f * MathF.PI;
        var attackRotation = random.Next(2);
        var gearPositionVector = MathUtilities.RotationToUnitVector(gearPlacementRotation).ToVector3(arenaMiddle.Y);

        var gearDropOmenDuration = 3.0f;
        var fullDelay = gearDropOmenDuration;

        // Drop gears
        for (var i = 0; i < totalGears; i++)
        {
            var distance = i switch
            {
                0 => gear1Distance,
                1 => gear2Distance,
                2 => gear3Distance,
                _ => 0,
            };
            var position = arenaMiddle + distance * gearPositionVector;
            if (i < 2 && EntityManager.TryCreateEntity<Circle>(out Entity circle))
            {
                circle
                    .Set(new Position(position))
                    .Set(new Scale(gearRadius * Vector3.One))
                    .Set(new Circle.Component(
                        gearDropOmenDuration,
                        0.0f,
                        null,
                        0.0f,
                        (e) =>
                        {
                            var player = Dalamud.ObjectTable.LocalPlayer;
                            if (player == null || player.IsDead) { return; }
                            if (player.HasTranscendance())
                            {
                                VfxSpawn.PlayInvulnerabilityEffect(player);
                            }
                            else
                            {
                                Pacify.ApplyToTarget(e, 60.0f);
                            }
                        })
                    );
                attacks.Add(circle);
            }

            var j = i;
            var action = DelayedAction.Create(World, () =>
            {
                var rotation = j switch
                {
                    0 => 0,
                    1 => 1.0f / 16.0f * MathF.PI,
                    2 => 0,
                    _ => 0,
                };
                rotation += gearPlacementRotation + MathF.PI;
                if (EntityManager.TryCreateEntity<GearBeam>(out Entity gear))
                {
                    gear
                        .Set(new Position(position))
                        .Set(new Rotation(rotation))
                        .Set(new GearBeam.Component(gearRadius))
                        .Add<Attack>();
                    attacks.Add(gear);
                    if (j == 2)
                    {
                        position += 5.0f * Vector3.UnitY;
                        gear.Set(new Position(position));
                        var fr = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation);
                        fr *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.5f * MathF.PI);
                        gear.Set(new FullRotation(fr));
                    }

                    // Puddle
                    Entity puddle = default;
                    if (j < 2 && EntityManager.TryCreateEntity<Puddle>(out puddle))
                    {
                        puddle
                            .Set(new Position(position))
                            .Set(new Scale(gearRadius * Vector3.One))
                            .Set(new Puddle.Component(
                                "bgcommon/world/common/vfx_for_btl/b0994/eff/b0994yuka1_o.avfx", 0.2f,
                                1.0f, (e) => { Pacify.ApplyToTarget(e, 60.0f); }));
                        attacks.Add(puddle);
                    }

                    // Attack Omen
                    var attackOmenDelay = 0.75f;
                    var attackOmenDuration = 5.5f;
                    var action = DelayedAction.Create(World, () =>
                    {
                        var ar = attackRotation;
                        if (totalGears % 2 == 1) { ar = 1 - ar; }
                        var rotationOmenVfxPath = ar == 0 ? "vfx/lockon/eff/m1001_turning_left01w.avfx" : "vfx/lockon/eff/m1001_turning_right01w.avfx";
                        // Put this at a scale of 0 just for the sound effect volume boost
                        var rotationOmen2VfxPath = ar == 0 ? "vfx/lockon/eff/m0973_turning_left_5sec_c0e1.avfx" : "vfx/lockon/eff/m0973_turning_right_5sec_c0e1.avfx";
                        if (j == 0)
                        {
                            gear.Set(new GearBeam.Beam(attackOmenDuration));
                        }
                        else if (index == 1 && j == 1)
                        {
                            var rotationOmen = FakeActor.Create(World)
                                .Set(new Position(position))
                                .Set(new Rotation(rotation))
                                .Set(new ActorVfx(rotationOmenVfxPath));
                            attacks.Add(rotationOmen);

                            var rotationOmen2 = FakeActor.Create(World)
                                .Set(new Model(0))
                                .Set(new Position(position))
                                .Set(new Rotation(rotation))
                                .Set(new UniformScale(0))
                                .Set(new ActorVfx(rotationOmen2VfxPath))
                                .Add<EnsureModelIsDrawn>();
                            attacks.Add(rotationOmen2);
                        }
                        else if (j == 2)
                        {
                            position += 0.5f * gearPositionVector;
                            var fr = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation);
                            fr *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);
                            var rotationOmen = FakeActor.Create(World)
                                .Set(new Model(0))
                                .Set(new Position(position))
                                .Set(new Rotation(rotation))
                                .Set(new Alpha(0))
                                .Set(new ActorVfx(rotationOmenVfxPath))
                                .Set(new FullRotation(fr))
                                .Add<EnsureModelIsDrawn>();
                            attacks.Add(rotationOmen);

                            var rotationOmen2 = FakeActor.Create(World)
                                .Set(new Model(0))
                                .Set(new Position(position))
                                .Set(new Rotation(rotation))
                                .Set(new UniformScale(0))
                                .Set(new ActorVfx(rotationOmen2VfxPath))
                                .Add<EnsureModelIsDrawn>();
                            attacks.Add(rotationOmen2);
                        }

                        // Execute attack
                        var attackDuration = 6.0f;
                        var rotationVelocity = 1.0f / attackDuration * MathF.PI * (attackRotation == 0 ? 1 : -1);
                        var action = DelayedAction.Create(World, () =>
                        {
                            if (j == 0)
                            {
                                gear.Set(new AngularVelocity(-rotationVelocity));
                            }
                            else if (j == 1)
                            {
                                gear.Set(new AngularVelocity(rotationVelocity));
                            }
                            else if (j == 2)
                            {
                                var fav = -rotationVelocity * Vector3.Normalize(MathUtilities.RotationToUnitVector(rotation).ToVector3(0));
                                gear.Set(new FullAngularVelocity(fav));
                            }

                            // Stop attack
                            var action = DelayedAction.Create(World, () =>
                            {
                                gear.Remove<GearBeam.Beam>();
                                gear.Remove<AngularVelocity>();
                                gear.Remove<FullAngularVelocity>();

                                // Cleanup
                                var cleanupDelay = 0.25f;
                                var action = DelayedAction.Create(World, () =>
                                {
                                    gear.SafeDestruct();
                                    puddle.SafeDestruct();
                                }, cleanupDelay);
                                attacks.Add(action);
                            }, attackDuration);
                            attacks.Add(action);
                        }, attackOmenDuration);
                        attacks.Add(action);
                    }, attackOmenDelay);
                    attacks.Add(action);
                }
            }, fullDelay);
            attacks.Add(action);
        }
    }
}
