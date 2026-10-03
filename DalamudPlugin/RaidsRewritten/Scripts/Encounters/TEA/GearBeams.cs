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
        if (source.BaseId == ALEXANDER_PRIME_BASE_ID && command == 54 && p1 == 1 && timesExecuted == 0)
        {
            StartMechanic();
            timesExecuted++;
        }
    }

    public override void DebugSimulate()
    {
        StartMechanic();
    }

    private void StartMechanic()
    {
        var seed = RngSeed;
        unchecked
        {
            seed += 0x4EA5;
        }
        var random = new Random(seed);

        var gear1Distance = 11.5f;
        var gear2Distance = 20.0f;
        var gearRadius = 5.0f;
        var gearPlacementRotation = random.Next(8) * 0.25f * MathF.PI;
        var gearPositionVector = MathUtilities.RotationToUnitVector(gearPlacementRotation).ToVector3(arenaMiddle.Y);

        var gearDropOmenDuration = 3.0f;
        var fullDelay = gearDropOmenDuration;

        // Drop gears
        for (var i = 0; i < 2; i++)
        {
            var distance = i == 0 ? gear1Distance : gear2Distance;
            var position = arenaMiddle + distance * gearPositionVector;
            if (EntityManager.TryCreateEntity<Circle>(out Entity circle))
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

                var j = i;
                var action1 = DelayedAction.Create(World, () =>
                {
                    var rotation = j == 0 ? 0 : (1.0f / 16.0f * MathF.PI);
                    rotation += gearPlacementRotation + MathF.PI;
                    if (EntityManager.TryCreateEntity<GearBeam>(out Entity gear))
                    {
                        gear
                            .Set(new Position(position))
                            .Set(new Rotation(rotation))
                            .Set(new GearBeam.Component(gearRadius))
                            .Add<Attack>();
                        attacks.Add(gear);

                        // Puddle
                        if (EntityManager.TryCreateEntity<Puddle>(out var puddle))
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
                        var attackOmenDuration = 5.0f;
                        var attackRotation = random.Next(2);
                        var action = DelayedAction.Create(World, () =>
                        {
                            if (j == 0)
                            {
                                gear.Set(new GearBeam.Beam(attackOmenDuration));
                            }
                            else if (j == 1)
                            {
                                var rotationOmenVfxPath = attackRotation == 0 ? "vfx/lockon/eff/m1001_turning_left01w.avfx" : "vfx/lockon/eff/m1001_turning_right01w.avfx";
                                var rotationOmen = FakeActor.Create(World)
                                    .Set(new Position(position))
                                    .Set(new ActorVfx(rotationOmenVfxPath));
                                attacks.Add(rotationOmen);

                                // Put this under the ground just for the sound effect volume boost
                                var rotationOmen2VfxPath = attackRotation == 0 ? "vfx/lockon/eff/m0973_turning_left_5sec_c0e1.avfx" : "vfx/lockon/eff/m0973_turning_right_5sec_c0e1.avfx";
                                var rotationOmen2 = FakeActor.Create(World)
                                    .Set(new Position(position - 6.0f * Vector3.UnitY))
                                    .Set(new ActorVfx(rotationOmen2VfxPath));
                                attacks.Add(rotationOmen2);
                            }

                            // Execute attack
                            var attackDuration = 6.0f;
                            var rotationVelocity = 1.0f / attackDuration * MathF.PI * (attackRotation == 0 ? 1 : -1);
                            var action = DelayedAction.Create(World, () =>
                            {
                                if (j == 0)
                                {
                                    rotationVelocity *= -1;
                                    gear.Set(new AngularVelocity(rotationVelocity));
                                }
                                else if (j == 1)
                                {
                                    gear.Set(new AngularVelocity(rotationVelocity));
                                }

                                // Stop attack
                                var action = DelayedAction.Create(World, () =>
                                {
                                    gear.Remove<GearBeam.Beam>();
                                    gear.Remove<AngularVelocity>();

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
                attacks.Add(action1);
            }
        }
    }
}
