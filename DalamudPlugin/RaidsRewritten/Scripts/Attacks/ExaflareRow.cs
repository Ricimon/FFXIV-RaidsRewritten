using Flecs.NET.Bindings;
using Flecs.NET.Core;
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
    public record struct Component(float ElapsedTime, List<int>? ExaflarePositions = null, int ExaflarePair = 0);

    private readonly DalamudServices dalamud = dalamud;
    private readonly ILogger logger = logger;
    private static readonly Random random = new();

    private const float ExaflareInterval = 3f;
    private const int ExaflarePairLimit = 2;
    private const float ExaflareOffsetYalms = 8;

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
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);

                if (ShouldDestruct(world, component, entity))
                {
                    entity.Destruct();
                    return;
                }

                if (component.ExaflarePair > ExaflarePairLimit) { return; }
                if (component.ElapsedTime < component.ExaflarePair * ExaflareInterval) { return; }

                if (component.ExaflarePositions == null)
                {
                    // index = spawn order
                    // value = position of exa in line
                    var list = Enumerable.Range(0, 6).ToList();

                    // shuffle list
                    for (int num = list.Count - 1; num > 1; num--)
                    {
                        int rnd = random.Next(num + 1);

                        (list[num], list[rnd]) = (list[rnd], list[num]);
                    }
                    component.ExaflarePositions = list;
                }

                // c# doesn't like refs in anonymous functions
                var originalPosition = position.Value;
                var originalRotation = rotation.Value;

                // calculate exa positions
                var idx = component.ExaflarePair * 2;
                component.ExaflarePair++;

                var exaPos1 = CalculateExaflarePosition(component.ExaflarePositions[idx], originalPosition, originalRotation);
                var exaPos2 = CalculateExaflarePosition(component.ExaflarePositions[idx + 1], originalPosition, originalRotation);

                entity.Scope(() =>
                {
                    Exaflare.CreateEntity(world)
                        .Set(new Position(exaPos1))
                        .Set(new Rotation(originalRotation));

                    Exaflare.CreateEntity(world)
                        .Set(new Position(exaPos2))
                        .Set(new Rotation(originalRotation));
                });
            });
    }

    private static bool ShouldDestruct(World world, Component component, Entity e) => component.ElapsedTime > 20 || (component.ExaflarePair > 0 && !HasChild(world, e));

    private static bool HasChild(World world, Entity e)
    {
        using var q = world.QueryBuilder().With(flecs.EcsChildOf, e).Build();
        return q.Count() > 0;
    }

    private static Vector3 CalculateExaflarePosition(int exaflarePosition, Vector3 position, float rotation)
    {
        // 2.5 is the middlepoint of starting exaflare positions
        position.X -= ExaflareOffsetYalms * MathF.Cos(rotation) * (exaflarePosition - 2.5f);
        position.Z += ExaflareOffsetYalms * MathF.Sin(rotation) * (exaflarePosition - 2.5f);

        return position;
    }
}
