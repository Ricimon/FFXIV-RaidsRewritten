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

public class ExaflareRow(DalamudServices dalamud, ILogger logger) : IAttack, ISystem
{
    public record struct Component(bool Played = false);

    private readonly DalamudServices dalamud = dalamud;
    private readonly ILogger logger = logger;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                try
                {
                    if (component.Played) { return; }
                    component.Played = true;

                    var list = Enumerable.Range(0, 6).ToList();

                    // shuffle list
                    Random random = new Random();
                    for (int num = list.Count - 1; num > 1; num--)
                    {
                        int rnd = random.Next(num + 1);

                        (list[num], list[rnd]) = (list[rnd], list[num]);
                    }

                    // c# doesn't like refs in anonymous functions
                    var originalPosition = position.Value;
                    var originalRotation = rotation.Value;

                    // calculate exa positions
                    for (var num = 0; num < list.Count; num += 2)
                    {
                        var exa1 = list[num];
                        var exa2 = list[num + 1];

                        DelayedAction.Create(world, () => CreateExaflare(exa1, world, originalPosition, originalRotation), num * 1.5f);
                        DelayedAction.Create(world, () => CreateExaflare(exa2, world, originalPosition, originalRotation), num * 1.5f);
                    }
                } catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }

                it.Entity(i).Destruct();
            });
    }

    private void CreateExaflare(int exaflarePosition, World world, Vector3 originalPosition, float originalRotation)
    {
        Exaflare.CreateEntity(world)
            .Set(new Position(CalculateExaflarePosition(exaflarePosition, originalPosition, originalRotation)))
            .Set(new Rotation(originalRotation));
    }

    private Vector3 CalculateExaflarePosition(int exa, Vector3 originalPosition, float originalRotation)
    {
        // TODO: understand geometry behind this
        // negative vs positive deg value on 45 deg
        var xUnit = 8 * MathF.Cos(-originalRotation);
        var zUnit = 8 * MathF.Sin(-originalRotation);
        var newPos = originalPosition;

        if (exa <= 2)
        {
            var xOffset = xUnit * (3 - exa - 0.5f);
            var zOffset = zUnit * (3 - exa - 0.5f);
            newPos.X += xOffset;
            newPos.Z += zOffset;
        } else
        {
            var xOffset = xUnit * (exa - 2 - 0.5f);
            var zOffset = zUnit * (exa - 2 - 0.5f);
            newPos.X -= xOffset;
            newPos.Z -= zOffset;
        }
        return newPos;
    }
}
