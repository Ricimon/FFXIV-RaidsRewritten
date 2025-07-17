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
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class LightningCorridor(DalamudServices dalamud, ILogger logger) : IAttack, ISystem
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
    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Start, 0.5f },
        { Phase.Omen, 0.5f },
        { Phase.Snapshot, 0.2f },
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
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                component.ElapsedTime += it.DeltaTime();

                var totalPhaseDuration = GetTotalPhaseDuration(component.Phase);
                var entity = it.Entity(i);
                switch (component.Phase)
                {
                    case Phase.Start:
                        if (component.ElapsedTime >= totalPhaseDuration)
                        {
                            component.Phase = Phase.Omen;

                            var omen1 = RectangleOmen.CreateEntity(it.World());
                            {
                                var r = MathUtilities.ClampRadians(rotation.Value + 0.5f * MathF.PI);
                                var p = position.Value + (0.5f * Width * MathUtilities.RotationToUnitVector(r)).ToVector3(0);
                                omen1.Set(new Position(p))
                                    .Set(new Rotation(r))
                                    .Set(new Scale(40.0f * Vector3.One))
                                    .ChildOf(entity);
                            }
                            var omen2 = RectangleOmen.CreateEntity(it.World());
                            {
                                var r = MathUtilities.ClampRadians(rotation.Value - 0.5f * MathF.PI);
                                var p = position.Value + (0.5f * Width * MathUtilities.RotationToUnitVector(r)).ToVector3(0);
                                omen2.Set(new Position(p))
                                    .Set(new Rotation(r))
                                    .Set(new Scale(40.0f * Vector3.One))
                                    .ChildOf(entity);
                            }
                        }
                        break;

                    case Phase.Omen:
                        if (component.ElapsedTime >= totalPhaseDuration)
                        {
                            component.Phase = Phase.Snapshot;

                            // Snapshot
                            var hitLocalPlayer = component.HitLocalPlayer;
                            entity.Children(child =>
                            {
                                if (!child.Has<Omen>()) { return; }

                                var localPlayer = dalamud.ClientState.LocalPlayer;
                                if (!hitLocalPlayer && localPlayer != null &&
                                    RectangleOmen.IsInOmen(child, localPlayer.Position))
                                {
                                    hitLocalPlayer = true;
                                }

                                child.Destruct();
                            });

                            component.HitLocalPlayer = hitLocalPlayer;
                        }
                        break;

                    case Phase.Snapshot:
                        if (component.ElapsedTime >= totalPhaseDuration)
                        {
                            component.Phase = Phase.Attack;

                            // Play VFX
                            // left
                            var r1 = MathUtilities.ClampRadians(rotation.Value + 0.5f * MathF.PI);
                            for (var j = 0; j < 2; j++)
                            {
                                var p1 = position.Value + ((0.5f * Width + 20.0f) * MathUtilities.RotationToUnitVector(r1)).ToVector3(0);
                                p1 += (-20.0f + j * 40.0f) * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0);
                                var fakeActor1 = FakeActor.Create(it.World())
                                    .Set(new Position(p1))
                                    .Set(new Rotation(rotation.Value))
                                    .Set(new ActorVfx("vfx/monster/gimmick5/eff/x6r4_b_g016_c0t1.avfx"))
                                    .ChildOf(entity);
                            }

                            // right
                            var r2 = MathUtilities.ClampRadians(rotation.Value - 0.5f * MathF.PI);
                            for (var j = 0; j < 2; j++)
                            {
                                var p2 = position.Value + ((0.5f * Width + 20.0f) * MathUtilities.RotationToUnitVector(r2)).ToVector3(0);
                                p2 += (-20.0f + j * 40.0f) * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0);
                                var fakeActor2 = FakeActor.Create(it.World())
                                    .Set(new Position(p2))
                                    .Set(new Rotation(rotation.Value))
                                    .Set(new ActorVfx("vfx/monster/gimmick5/eff/x6r4_b_g016_c0t1.avfx"))
                                    .ChildOf(entity);
                            }

                            // Affect player
                            if (component.HitLocalPlayer)
                            {
                                using var q = Player.Query(it.World());
                                q.Each((Entity e, ref Player.Component _) =>
                                {
                                    Bind.ApplyToPlayer(e, 2.0f);
                                });
                            }
                        }
                        break;

                    case Phase.Attack:
                        entity.Scope(() =>
                        {
                            if (!it.World().Query<ActorVfx>().IsTrue())
                            {
                                entity.Destruct();
                            }
                        });

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
