using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class OctetDonut (DalamudServices dalamud, Random random, CommonQueries commonQueries, ILogger logger) : IAttack, ISystem
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
        { Phase.Snapshot, 4.5f },
        { Phase.SpawnObstacleCourse, 5f },
        { Phase.Destruct, 60f }
    };

    public record struct Component(float ElapsedTime, Phase Phase = Phase.Omen);
    public record struct TornadoDirection(bool IsClockwise);

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
                        .ChildOf(entity);

                    component.Phase = Phase.Snapshot;
                    break;
                case Phase.Snapshot:
                    if (ShouldReturn(component)) { return; }

                    var player = dalamud.ClientState.LocalPlayer;
                    if (player == null) { return; }

                    var fakeActor = FakeActor.Create(world)
                        .Set(new Position(position.Value))
                        .Set(new ActorVfx(AoEVfx))
                        .ChildOf(entity);

                    entity.Children((Entity child) =>
                    {
                        if (!child.Has<Omen>()) { return; }

                        if (OneThirdDonutOmen.IsInOmen(child, player.Position))
                        {
                            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                            {
                                Stun.ApplyToTarget(e, StunDuration);
                            });
                        }
                        child.Destruct();
                    });

                    component.Phase = Phase.SpawnObstacleCourse;
                    break;
                case Phase.SpawnObstacleCourse:
                    if (ShouldReturn(component)) { return; }

                    var randBool = random.Next() % 2 == 1;
                    var randAngle = MathHelper.DegToRad(random.Next(359));
                    var p1 = new Vector3(
                            position.Value.X + OuterTornadoDistance * MathF.Cos(randAngle),
                            position.Value.Y,
                            position.Value.Z + OuterTornadoDistance * MathF.Sin(randAngle)
                        );
                    var tornado1 = Tornado.CreateEntity(world)
                        .Set(new Position(p1))
                        .Set(new TornadoDirection(randBool))
                        .ChildOf(entity);

                    randAngle = MathHelper.DegToRad(random.Next(359));
                    var p2 = new Vector3(
                            position.Value.X + InnerTornadoDistance * MathF.Cos(randAngle),
                            position.Value.Y,
                            position.Value.Z + InnerTornadoDistance * MathF.Sin(randAngle)
                        );
                    var tornado2 = Tornado.CreateEntity(world)
                        .Set(new Position(p2))
                        .Set(new TornadoDirection(!randBool))
                        .ChildOf(entity);

                    var twisterPachinko = TwisterObstacleCourse.CreateEntity(world)
                        .Set(new Position(position.Value))
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

                var angle = MathUtilities.GetAbsoluteAngleFromSourceToTarget(arenaCenter.Value, position.Value);
                angle = MathUtilities.ClampRadians(angle + MathF.PI / 2 * (direction.IsClockwise ? 1 : -1));

                // base * delta * angular velocity ratio
                var velocity = BaseTornadoSpeed * it.DeltaTime();

                var newPos = new Vector3(
                        position.Value.X + MathF.Sin(angle) * velocity,
                        position.Value.Y,
                        position.Value.Z + MathF.Cos(angle) * velocity
                    );

                entity.Set(new Position(newPos));
            });
    }

    private bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase];
}
