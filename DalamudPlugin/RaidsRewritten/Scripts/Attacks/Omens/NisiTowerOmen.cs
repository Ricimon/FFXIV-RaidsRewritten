using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class NisiTowerOmen(DalamudServices dalamud, ILogger logger) : IEntity, ISystem
{
    public const string NisiAlphaVfxPath = "vfx/common/eff/m0598_stlp6c0c.avfx";
    public const string NisiBetaVfxPath = "vfx/common/eff/m0598_stlp7c0c.avfx";
    public const string NisiGammaVfxPath = "vfx/common/eff/m0598_stlp8c0c.avfx";
    public const string NisiDeltaVfxPath = "vfx/common/eff/m0598_stlp9c0c.avfx";

    private const float Radius = 3.0f;
    private const float TimeToSnapshot = 8.0f;

    public enum Nisi : byte
    {
        None = 0,
        Alpha = 1,
        Beta = 2,
        Gamma = 3,
        Delta = 4,
    }

    public record struct Component(Nisi NisiType, Entity NisiVfx = default, Entity TowerFilledVfx = default, float ElapsedTime = 0);
    public struct UseLocalPlayerPosition;
    private record struct NisiVfx(float RotationInterval);

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("bg/ex2/05_zon_z3/common/vfx/eff/b1512pil01_u.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Set(new InTowerOmen())
            .Add<Attack>()
            .Add<Omen>();
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                if (!component.NisiVfx.IsValid())
                {
                    var nisiVfxPath = component.NisiType switch
                    {
                        Nisi.Alpha => NisiAlphaVfxPath,
                        Nisi.Beta => NisiBetaVfxPath,
                        Nisi.Gamma => NisiGammaVfxPath,
                        Nisi.Delta => NisiDeltaVfxPath,
                        _ => string.Empty,
                    };

                    if (!string.IsNullOrEmpty(nisiVfxPath))
                    {
                        component.NisiVfx = FakeActor.Create(it.World())
                            .Set(new ActorVfx(nisiVfxPath))
                            .Set(new LocalPosition())
                            .Set(new Rotation())
                            .Set(new NisiVfx(5.0f))
                            .ChildOf(it.Entity(i));
                    }
                    else
                    {
                        component.NisiVfx = it.World().Entity().ChildOf(it.Entity(i));
                    }
                }

                component.ElapsedTime += it.DeltaTime();

                if (component.ElapsedTime > 10.0f)
                {
                    it.Entity(i).Destruct();
                }
            });

        world.System<NisiVfx, LocalPosition, Component>()
            .TermAt(2).Up()
            .Each((Iter it, int i, ref NisiVfx nisiVfx, ref LocalPosition localPosition, ref Component component) =>
            {
                if (component.ElapsedTime > TimeToSnapshot)
                {
                    it.Entity(i).Destruct();
                    return;
                }

                var rotation = component.ElapsedTime / nisiVfx.RotationInterval * 2 * MathF.PI + 0.475f * MathF.PI;

                var parent = it.Entity(i).Parent();
                if (parent.IsValid())
                {
                    if (parent.TryGet(out Rotation parentRotation) &&
                        parent.TryGet(out Scale parentScale))
                    {
                        var direction = MathUtilities.Rotate(Vector2.UnitX, parentRotation.Value + rotation).ToVector3(0);
                        // Experimental code for non-uniform scale
                        //var parentScaleValue = MathUtilities.Rotate(parentScale.Value.ToVector2(), parentRotation.Value).ToVector3(0);
                        //direction = Vector3.Multiply(direction, Vector3.Abs(parentScaleValue));
                        //localPosition.Value = Radius * direction + -1.5f * Vector3.UnitY;
                        localPosition.Value = parentScale.Value.X * Radius * direction + -1.5f * Vector3.UnitY;
                    }
                }
            });

        world.System<Component, InTowerOmen, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref InTowerOmen inTower, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                if (component.ElapsedTime < TimeToSnapshot && inTower.Count > 0)
                {
                    if (!component.TowerFilledVfx.IsValid())
                    {
                        component.TowerFilledVfx = it.World().Entity()
                            .Set(new StaticVfx("bg/ex2/05_zon_z3/common/vfx/eff/b1512pil02_u.avfx"))
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(scale.Value))
                            .Add<Attack>()
                            .Add<Omen>()
                            .ChildOf(it.Entity(i));
                    }
                }
                else
                {
                    component.TowerFilledVfx.SafeDestruct();
                }
            });

        world.System<InTowerOmen, Position, Rotation, Scale>()
            .With<Component>()
            .With<UseLocalPlayerPosition>()
            .Each((ref InTowerOmen inTower, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                inTower.Count = 0;
                var player = dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (Vector3.Distance(position.Value, player.Position) <= scale.Value.X * Radius)
                    {
                        inTower.Count = 1;
                    }
                    // Experimental code for non-uniform scale
                    //var toPlayer = (player.Position - position.Value).ToVector2();
                    //if (toPlayer.LengthSquared() == 0)
                    //{
                    //    inTower.Count = 1;
                    //    return;
                    //}

                    //var rotatedScale = MathUtilities.Rotate(scale.Value.ToVector2(), rotation.Value);
                    //var maxVectorToPlayer = Vector2.Multiply(Radius * Vector2.Normalize(toPlayer), Vector2.Abs(rotatedScale));
                    //if (toPlayer.LengthSquared() <= maxVectorToPlayer.LengthSquared())
                    //{
                    //    inTower.Count = 1;
                    //}
                }
            });
    }
}
