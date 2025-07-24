using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class Circle(DalamudServices dalamud, ILogger logger) : IAttack, ISystem
{
    public record struct Component(Action<Entity> OnHit);

    public readonly DalamudServices dalamud = dalamud;
    public readonly ILogger logger = logger;

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

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                try
                {
                    var player = this.dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    var distanceToCenter = Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2());
                    var onHit = component.OnHit;

                    if (distanceToCenter < scale.Value.Z)
                    {
                        using var q = Player.Query(it.World());
                        q.Each((Entity e, ref Player.Component _) =>
                        {
                            onHit(e);
                        });
                    }

                    it.Entity(i).Destruct();
                } catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
