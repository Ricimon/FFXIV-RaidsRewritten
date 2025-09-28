using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class OctetDonut (DalamudServices dalamud, Random random, CommonQueries commonQueries, VfxSpawn vfxSpawn, ILogger logger) : IEntity, ISystem
{
    public enum Phase
    {
        Omen,
        Snapshot,
        SpawnObstacleCourse,
        Destruct
    }

    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Omen, 0 },
        { Phase.Snapshot, 4.75f },
        { Phase.SpawnObstacleCourse, 5f },
        { Phase.Destruct, 60f }
    };

    public record struct Component(float ElapsedTime, Phase Phase = Phase.Omen);
    public record struct TornadoDirection(bool IsClockwise, float Radius, float CurrentAngle);
    public record struct SeededRandom(Random Random);

    private const float OmenDuration = 4.75f;
    private const float BaseTornadoSpeed = 5.4f;
    private const float OuterTornadoDistance = 20.25f;
    private const float InnerTornadoDistance = 14.25f;
    private const string AoEVfx = "vfx/monster/gimmick4/eff/z5r2_b1_g01c0g.avfx";
    private const float StunDuration = 30f;
    private const int OmenScale = 40;


    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position>().Each((Iter it, int i, ref Component component, ref Position position) =>
        {
            component.ElapsedTime += it.DeltaTime();
            var entity = it.Entity(i);

            switch(component.Phase)
            {
                case Phase.Omen:
                    if (ShouldReturn(component)) { return; }

                    OneThirdDonutOmen.CreateEntity(world)
                        .Set(new Position(position.Value))
                        .Set(new Scale(new Vector3(OmenScale)))
                        .Set(new OmenDuration(OmenDuration, false))
                        .ChildOf(entity);

                    component.Phase = Phase.Snapshot;
                    break;
                case Phase.Snapshot:
                    if (ShouldReturn(component)) { return; }

                    var fakeActor = FakeActor.Create(world)
                        .Set(new Position(position.Value))
                        .Set(new ActorVfx(AoEVfx))
                        .ChildOf(entity);

                    entity.Children((Entity child) =>
                    {
                        if (!child.Has<Omen>()) { return; }

                        var player = dalamud.ClientState.LocalPlayer;
                        if (player != null && !player.IsDead)
                        {
                            if (OneThirdDonutOmen.IsInOmen(child, player.Position))
                            {
                                if (player.HasTranscendance())
                                {
                                    DelayedAction.Create(world, () =>
                                    {
                                        vfxSpawn.PlayInvulnerabilityEffect(player);
                                    }, 0.5f);
                                } else
                                {
                                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                    {
                                        DelayedAction.Create(world, () =>
                                        {
                                            Stun.ApplyToTarget(e, StunDuration);
                                        }, 0.5f);
                                    });
                                }
                            }
                        }

                        child.Destruct();
                    });

                    component.Phase = Phase.SpawnObstacleCourse;
                    break;
                case Phase.SpawnObstacleCourse:
                    if (ShouldReturn(component)) { return; }

                    Random rand = entity.Has<SeededRandom>() ? entity.Get<SeededRandom>().Random : random;
                    var randBool = rand.Next() % 2 == 1;
                    var randAngle = MathHelper.DegToRad(rand.Next(360));
                    var p1 = new Vector3(
                            position.Value.X + OuterTornadoDistance * MathF.Sin(randAngle),
                            position.Value.Y,
                            position.Value.Z + OuterTornadoDistance * MathF.Cos(randAngle)
                        );
                    var tornado1 = Tornado.CreateEntity(world)
                        .Set(new Position(p1))
                        .Set(new TornadoDirection(randBool, OuterTornadoDistance, randAngle))
                        .ChildOf(entity);

                    randAngle = MathHelper.DegToRad(rand.Next(360));
                    var p2 = new Vector3(
                            position.Value.X + InnerTornadoDistance * MathF.Cos(randAngle),
                            position.Value.Y,
                            position.Value.Z + InnerTornadoDistance * MathF.Sin(randAngle)
                        );
                    var tornado2 = Tornado.CreateEntity(world)
                        .Set(new Position(p2))
                        .Set(new TornadoDirection(!randBool, InnerTornadoDistance, randAngle))
                        .ChildOf(entity);

                    var twisterPachinko = TwisterObstacleCourse.CreateEntity(world)
                        .Set(new Position(position.Value))
                        .Set(new SeededRandom(rand))
                        .ChildOf(entity);

                    component.Phase = Phase.Destruct;
                    break;
                case Phase.Destruct:
                    if (ShouldReturn(component)) { return; }
                    entity.Destruct();
                    break;
            }
        });

        world.System<TornadoDirection, Position, Position>().TermAt(2).Up()
            .Each((Iter it, int i, ref TornadoDirection direction, ref Position position, ref Position arenaCenter) =>
            {
                var entity = it.Entity(i);
                var deltaAngle = it.DeltaTime() * BaseTornadoSpeed / direction.Radius * (direction.IsClockwise ? 1 : -1);
                var newAngle = direction.CurrentAngle - deltaAngle;
                var offsetX = direction.Radius * MathF.Sin(newAngle);
                var offsetZ = direction.Radius * MathF.Cos(newAngle);
                direction.CurrentAngle -= deltaAngle;
                var newPos = new Vector3(
                        arenaCenter.Value.X + offsetX,
                        position.Value.Y,
                        arenaCenter.Value.Z + offsetZ
                    );

                entity.Set(new Position(newPos));
            });
    }

    private bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase];
}
