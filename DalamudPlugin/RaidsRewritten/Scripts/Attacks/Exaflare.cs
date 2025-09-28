using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Exaflare(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IEntity, ISystem
{
    public enum Phase
    {
        Omen,
        OmenDestruct,
        Snapshot,
        Vfx,
        Destruct
    }

    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Omen, 0 },
        { Phase.OmenDestruct, 3.5f },
        { Phase.Destruct, 3f }
    };

    public record struct Component(float ElapsedTime, int CurrentExaNum = 0, Phase Phase = Phase.Omen);

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    private const float OmenPhase = 3.8f;
    private const float SnapshotOffset = 0.25f;
    private const float ExaflareInterval = 1.5f;
    private const float ExaflareSize = 6f;  // thanks tom
    private const string ExaflareVfxPath = "vfx/monster/gimmick2/eff/f1bz_b0_g02c0i.avfx";

    private const float StunDuration = 10f;
    private const float StatusDelay = 0.4f;
    private const float PacifyDuration = 20f;

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                component.ElapsedTime += it.DeltaTime();

                Entity fakeActor;
                var entity = it.Entity(i);

                {
                    using var childQuery = world.QueryBuilder().With(flecs.EcsChildOf, entity).With<FakeActor.Component>().Build();

                    if (!childQuery.IsTrue())
                    {
                        fakeActor = FakeActor.Create(it.World())
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .ChildOf(entity);
                    } else
                    {
                        fakeActor = childQuery.First();
                    }
                }

                switch (component.Phase)
                {
                    case Phase.Omen:
                        component.Phase = Phase.OmenDestruct;

                        var omen = ExaflareOmen.CreateEntity(world);
                        omen.Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(new Vector3(ExaflareSize)))
                            .ChildOf(entity);
                        break;
                    case Phase.OmenDestruct:
                        if (ShouldReturn(component)) { return; }
                        component.Phase = Phase.Snapshot;

                        entity.DestructChildEntity<Omen>();

                        break;
                    case Phase.Snapshot:
                        if (ShouldReturn(component)) { return; }
                        component.Phase = Phase.Vfx;

                        Vector3 newPos;
                        if (component.CurrentExaNum > 0)
                        {
                            newPos = new Vector3(
                                position.Value.X + (component.CurrentExaNum) * 8 * MathF.Sin(rotation.Value),
                                position.Value.Y,
                                position.Value.Z + (component.CurrentExaNum) * 8 * MathF.Cos(rotation.Value));
                            fakeActor.Set(new Position(newPos));
                        } else
                        {
                            newPos = position.Value;
                        }

                        Circle.CreateEntity(world)
                            .Set(new Position(newPos))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(new Vector3(ExaflareSize)))
                            .Set(new Circle.Component(OnHit))
                            .ChildOf(entity);

                        break;
                    case Phase.Vfx:
                        if (ShouldReturn(component)) { return; }
                        component.Phase = Phase.Snapshot;
                        component.CurrentExaNum++;

                        fakeActor.Set(new ActorVfx(ExaflareVfxPath));

                        if (component.CurrentExaNum > 5) {
                            component.Phase = Phase.Destruct;
                            component.ElapsedTime = 0;
                        }
                        break;
                    case Phase.Destruct:
                        if (ShouldReturn(component)) { return; }
                        entity.Destruct();
                        break;
                }
            });
    }

    private void OnHit(Entity e)
    {
        var player = dalamud.ClientState.LocalPlayer;
        if (player == null || player.IsDead) { return; }
        if (player.HasTranscendance())
        {
            DelayedAction.Create(e.CsWorld(), () => {
                vfxSpawn.PlayInvulnerabilityEffect(player);
            }, StatusDelay);
        }
        else
        {
            DelayedAction.Create(e.CsWorld(), () => {
                Stun.ApplyToTarget(e, StunDuration);
                Pacify.ApplyToTarget(e, PacifyDuration);
            }, StatusDelay);
        }
    }

    private bool ShouldReturn(Component component) {
        if (component.Phase == Phase.Vfx || component.Phase == Phase.Snapshot)
        {
            var ret = (component.ElapsedTime - OmenPhase) % ExaflareInterval < SnapshotOffset;
            return component.Phase == Phase.Vfx == ret;
        } else
        {
            return component.ElapsedTime < phaseTimings[component.Phase];
        }
    }
}
