using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class TwisterObstacleCourse (Random random) : IAttack, ISystem
{
    public record struct Component(int Sets, float OuterRadius);

    private const float OuterRadius = 22f;
    private const float TwisterRadius = 0.9f;
    private const float Spacing = 1.8f;
    private const int TwisterSlots = 5;
    private const int NumSets = 10;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Component(NumSets, OuterRadius))
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.Observer<Component, Position>().Event(Ecs.OnAdd)
            .Each((Iter it, int i, ref Component component, ref Position position) =>
            {
                var entity = it.Entity(i);
                Random rand = entity.Has<OctetDonut.SeededRandom>() ? entity.Get<OctetDonut.SeededRandom>().Random : random;
                var angleOffset = MathHelper.DegToRad(rand.Next(360));
                for (int currentPartialSet = 0; currentPartialSet < component.Sets * 2; currentPartialSet++)
                {
                    var angle = MathUtilities.ClampRadians(currentPartialSet * MathF.PI / component.Sets + angleOffset);
                    SpawnTwisters(entity, angle, position.Value, component, currentPartialSet % 2 == 0);
                }
            });
    }

    private static void SpawnTwisters(Entity parent, float angle, Vector3 center, Component component, bool rowType)
    {
        for (int i = 0; i < TwisterSlots; i++)
        {
            // determines which "twister slot" to skip
            // rowType == true => 3 twister row, false => 2 twister row
            if ((i % 2 == 0) == rowType) { continue; }

            var distanceFromCenter = component.OuterRadius - TwisterRadius - Spacing * i;
            var position = new Vector3(
                    center.X + distanceFromCenter * MathF.Cos(angle),
                    center.Y,
                    center.Z + distanceFromCenter * MathF.Sin(angle)
                );

            Twister.CreateEntity(parent.CsWorld())
                .Set(new Position(position))
                .ChildOf(parent);
        }
    }
}
