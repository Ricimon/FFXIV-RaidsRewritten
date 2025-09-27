using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public sealed class LightningCorridor(DalamudServices dalamud, CommonQueries commonQueries, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public enum Phase
    {
        Start,
        Omen,
        Snapshot,
        Attack,
    }

    public record struct Component(
        float ElapsedTime,
        Phase Phase = Phase.Start,
        bool HitLocalPlayer = false);

    private const float Width = 10.0f;
    private const int ParalysisId = 6401601;
    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Start, 0.0f },
        { Phase.Omen, 0.7f },
        { Phase.Snapshot, 0.0f },
        { Phase.Attack, 5.0f },
    };

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>().Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
        {
            component.ElapsedTime += it.DeltaTime();

            var totalPhaseDuration = GetTotalPhaseDuration(component.Phase);
            var entity = it.Entity(i);
            switch (component.Phase)
            {
                case Phase.Start:
                    if (component.ElapsedTime < totalPhaseDuration) { break; }
                    component.Phase = Phase.Omen;

                    var omenDuration = this.phaseTimings[Phase.Omen];
                    var omen1 = RectangleOmen.CreateEntity(it.World());
                    {
                        var r = MathUtilities.ClampRadians(rotation.Value + 0.5f * MathF.PI);
                        var p = position.Value + (0.5f * Width * MathUtilities.RotationToUnitVector(r)).ToVector3(0);
                        omen1.Set(new Position(p))
                            .Set(new Rotation(r))
                            .Set(new Scale(40.0f * Vector3.One))
                            .Set(new OmenDuration(omenDuration, false))
                            .ChildOf(entity);
                    }
                    var omen2 = RectangleOmen.CreateEntity(it.World());
                    {
                        var r = MathUtilities.ClampRadians(rotation.Value - 0.5f * MathF.PI);
                        var p = position.Value + (0.5f * Width * MathUtilities.RotationToUnitVector(r)).ToVector3(0);
                        omen2.Set(new Position(p))
                            .Set(new Rotation(r))
                            .Set(new Scale(40.0f * Vector3.One))
                            .Set(new OmenDuration(omenDuration, false))
                            .ChildOf(entity);
                    }
                    break;

                case Phase.Omen:
                    if (component.ElapsedTime < totalPhaseDuration) { break; }
                    component.Phase = Phase.Snapshot;

                    // Snapshot
                    var hitLocalPlayer = component.HitLocalPlayer;
                    entity.Children(child =>
                    {
                        if (!child.Has<Omen>()) { return; }

                        var player = dalamud.ClientState.LocalPlayer;
                        if (!hitLocalPlayer &&
                            player != null && !player.IsDead &&
                            RectangleOmen.IsInOmen(child, player.Position))
                        {
                            hitLocalPlayer = true;
                        }

                        child.Destruct();
                    });

                    component.HitLocalPlayer = hitLocalPlayer;
                    break;

                case Phase.Snapshot:
                    if (component.ElapsedTime < totalPhaseDuration) { break; }
                    component.Phase = Phase.Attack;

                    // Play VFX
                    // left
                    var r1 = MathUtilities.ClampRadians(rotation.Value + 0.5f * MathF.PI);
                    for (var j = 0; j < 2; j++)
                    {
                        var p = position.Value + ((0.5f * Width + 20.0f) * MathUtilities.RotationToUnitVector(r1)).ToVector3(0);
                        var pVfx = p + (-20.0f + j * 40.0f) * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0);
                        var fakeActor = FakeActor.Create(it.World())
                            .Set(new Position(pVfx))
                            .Set(new Rotation(rotation.Value))
                            .Set(new ActorVfx("vfx/monster/gimmick5/eff/x6r4_b_g016_c0t1.avfx"))
                            .ChildOf(entity);

                        // SFX
                        if (j == 0)
                        {
                            var fakeSfxActor = FakeActor.Create(it.World())
                                .Set(new Position(position.Value))
                                .Set(new Rotation(rotation.Value))
                                //.Set(new OneTimeModelTimeline(11179))
                                .Set(new OneTimeModelTimeline(11180))
                                .ChildOf(fakeActor);
                        }

                        // SFX played from an actor animation only plays at most 1 instance of a specific SFX,
                        // so multiple SFX types need to be played to get surround sound.
                        // But it was hard to find two different audios that sounded similar enough
                        // for this, so I'm opting to just put one audio source in the middle.
                    }

                    // right
                    var r2 = MathUtilities.ClampRadians(rotation.Value - 0.5f * MathF.PI);
                    for (var j = 0; j < 2; j++)
                    {
                        var p = position.Value + ((0.5f * Width + 20.0f) * MathUtilities.RotationToUnitVector(r2)).ToVector3(0);
                        var pVfx = p + (-20.0f + j * 40.0f) * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0);
                        var fakeActor = FakeActor.Create(it.World())
                            .Set(new Position(pVfx))
                            .Set(new Rotation(rotation.Value))
                            .Set(new ActorVfx("vfx/monster/gimmick5/eff/x6r4_b_g016_c0t1.avfx"))
                            .ChildOf(entity);
                    }

                    // Affect player
                    if (component.HitLocalPlayer)
                    {
                        var player = dalamud.ClientState.LocalPlayer;
                        if (player != null && !player.IsDead)
                        {
                            // This technically doesn't snapshot transcendance during snapshot calculation,
                            // but for this particular attack it's okay because the damage delay is so short
                            if (player.HasTranscendance())
                            {
                                vfxSpawn.PlayInvulnerabilityEffect(player);
                            }
                            else
                            {
                                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                {
                                    Paralysis.ApplyToTarget(e, 30.0f, 3.0f, 1.0f);
                                });
                            }
                        }
                    }
                    break;

                case Phase.Attack:
                    var childCount = 0;
                    entity.Children(_ => childCount++);
                    if (childCount == 0)
                    {
                        entity.Destruct();
                    }

                    // Failsafe
                    if (component.ElapsedTime >= totalPhaseDuration)
                    {
                        entity.Destruct();
                    }
                    break;
            }
        });
    }

    private float GetTotalPhaseDuration(Phase phase)
    {
        float duration = 0;
        while (phase >= 0)
        {
            if (phaseTimings.TryGetValue(phase, out var d))
            {
                duration += d;
            }
            phase -= 1;
        }
        return duration;
    }
}
