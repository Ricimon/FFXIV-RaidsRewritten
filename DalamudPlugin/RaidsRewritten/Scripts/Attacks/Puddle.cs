using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Puddle(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : IEntity, ISystem
{
    public record struct Component(
        string VfxPath,
        float VfxScaleMultiplier,
        float Cooldown,
        Action<Entity> OnHit,
        Entity Vfx = default);

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
                var entity = it.Entity(i);
                if (!component.Vfx.IsValid())
                {
                    component.Vfx = it.World().Entity()
                        .Set(new StaticVfx(component.VfxPath))
                        .ChildOf(entity);
                }
                if (component.Vfx.IsValid())
                {
                    component.Vfx
                        .Set(new Position(position.Value))
                        .Set(new Rotation(rotation.Value))
                        .Set(new Scale(component.VfxScaleMultiplier * scale.Value));
                }

                component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);

                if (component.Cooldown > 0) { return; }

                var player = dalamud.ObjectTable.LocalPlayer;
                if (player == null || player.IsDead || player.HasTranscendance()) { return; }

                if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= scale.Value.X)
                {
                    component.Cooldown = 3.0f;

                    var onHit = component.OnHit;
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        onHit(e);
                    });
                }
            });
    }
}
