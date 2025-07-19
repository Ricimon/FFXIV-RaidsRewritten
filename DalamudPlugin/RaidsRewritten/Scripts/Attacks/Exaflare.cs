using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class Exaflare(DalamudServices dalamud,ILogger logger) : IAttack, ISystem
{
    public record struct Component(float ElapsedTime, int CurrentExaNum = 0, Entity? FakeActor = null);
    
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

    private readonly float OmenVisibleSeconds = 3f;
    private readonly float ExaflareInterval = 1.5f;
    private readonly float ExaflareSize = 6.5f;  // thanks tom
    private readonly string ExaflareVfxPath = "vfx/monster/gimmick2/eff/f1bz_b0_g02c0i.avfx";

    private readonly float StunDuration = 10f;
    private readonly float StunDelay = 0.2f;

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);
                var exaflareThreshold = OmenVisibleSeconds + (component.CurrentExaNum - 1) * ExaflareInterval;
                if (component.CurrentExaNum <= 0)
                {
                    component.CurrentExaNum = 1;

                    var omen = ExaflareOmen.CreateEntity(it.World());
                    omen.Set(new Position(position.Value))
                        .Set(new Rotation(rotation.Value))
                        .Set(new Scale(new Vector3(ExaflareSize)))
                        .ChildOf(entity);
                } else if (component.CurrentExaNum < 7)
                {
                    if (component.ElapsedTime < exaflareThreshold) { return; }

                    entity.Children(child => child.Destruct());

                    Vector3 newPos;

                    if (!component.FakeActor.HasValue)
                    {
                        component.FakeActor = FakeActor.Create(it.World())
                            .Set(new Rotation(rotation.Value));
                        newPos = position.Value;
                    } else
                    {
                        newPos = new Vector3(
                            position.Value.X + (component.CurrentExaNum - 1) * 8 * MathF.Sin(rotation.Value),
                            position.Value.Y,
                            position.Value.Z + (component.CurrentExaNum - 1) * 8 * MathF.Cos(rotation.Value));
                    }

                    void OnHit(Entity e)
                    {
                        using var bindQuery = world.Query<Condition.Component, Bind.Component>();
                        if (!bindQuery.IsTrue())
                            DelayedAction.Create(e.CsWorld(), () => Bind.ApplyToPlayer(e, StunDuration), StunDelay);
                    }

                    Circle.CreateEntity(world)
                        .Set(new Position(newPos))
                        .Set(new Rotation(rotation.Value))
                        .Set(new Scale(new Vector3(ExaflareSize)))
                        .Set(new Circle.Component(OnHit));

                    var fakeActor = component.FakeActor.Value;
                    fakeActor.Set(new Position(newPos))
                        .Set(new ActorVfx(ExaflareVfxPath));

                    component.CurrentExaNum++;
                } else if (component.CurrentExaNum >= 7)
                {
                    if (component.ElapsedTime < exaflareThreshold) { return; }

                    component.FakeActor?.Destruct();
                    entity.Destruct();
                }
            });
    }
}
