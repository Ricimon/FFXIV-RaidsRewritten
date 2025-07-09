using System;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public unsafe class RollingBall(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float TimeUntilRolling, bool EntryAnimationPlayed = false, float TargetYPosition = default, float Speed = 0);

    private readonly DalamudServices dalamud = dalamud;
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    private const float AnimationSpeed = 1.75f;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(1443))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(0.6f))
            .Set(new Component(2.25f))
            .Add<Attack>();
    }

    public void Register(World world)
    {
        // Make the ball roll around
        world.System<Model, Component, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                if (!model.Spawned) { return; }

                if (!component.EntryAnimationPlayed)
                {
                    component.EntryAnimationPlayed = true;

                    component.TargetYPosition = position.Value.Y;

                    // Spawn the ball high up
                    var p = position.Value;
                    p.Y += 5.0f;
                    position.Value = p;
                }

                if (position.Value.Y != component.TargetYPosition)
                {
                    // Bring the ball down
                    var p = position.Value;
                    p.Y -= 50.0f * it.DeltaTime();
                    if (p.Y <= component.TargetYPosition)
                    {
                        p.Y = component.TargetYPosition;

                        if (model.GameObject != null)
                        {
                            this.vfxSpawn.SpawnActorVfx("vfx/pop/m0318/eff/m0318_pop01h.avfx", model.GameObject, model.GameObject);
                        }
                    }
                    position.Value = p;
                    return;
                }

                if (component.TimeUntilRolling > 0)
                {
                    component.TimeUntilRolling = Math.Max(component.TimeUntilRolling - it.DeltaTime(), 0);
                }

                // The rolling animation takes a little time to startup
                if (component.TimeUntilRolling < 0.07f)
                {
                    var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
                    var chara = (Character*)obj;
                    if (chara != null)
                    {
                        chara->Timeline.BaseOverride = 41;
                    }
                    //it.Entity(i).Set(new ModelTimelineSpeed(AnimationSpeed));
                }

                if (component.TimeUntilRolling > 0) { return; }

                // Accelerate
                var acceleration = 25.0f;
                var maxSpeed = 8.75f;
                var speed = component.Speed;
                speed = Math.Clamp(speed + acceleration * it.DeltaTime(), 0, maxSpeed);
                position.Value += MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0) * speed * it.DeltaTime();

                component.Speed = speed;
                it.Entity(i).Set(new ModelTimelineSpeed(speed / maxSpeed * AnimationSpeed));
            });
    }
}
