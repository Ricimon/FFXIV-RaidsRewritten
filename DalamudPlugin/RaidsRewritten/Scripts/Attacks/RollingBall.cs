using System;
using System.Numerics;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public unsafe sealed class RollingBall(DalamudServices dalamud, VfxSpawn vfxSpawn, Random random, ILogger logger) : IAttack, ISystem, IDisposable
{
    public record struct Component(float TimeUntilRolling, bool EntryAnimationPlayed = false, float TargetYPosition = default, float Cooldown = default);
    public record struct Movement(Vector2 Direction, float Speed = 0, double SimulationBufferTime = 0);
    public record struct SeededRandom(Random Random);
    public record struct CircleArena(Vector2 Center, float Radius);
    public record struct SquareArena(Vector2 Center, float Width);
    public record struct ShowOmen(Entity Omen);

    private const float MaxSpeed = 8.75f;
    private const float AnimationSpeed = 1.75f;
    private const float HitboxRadius = 4.0f;
    private const float HitCooldown = 0.25f;
    private const float KnockbackDuration = 1.0f;
    private const float ReflectAngleVariance = 25.0f; // degrees
    private const double FixedDeltaTime = 0.01f;

    private Query<Player.Component> playerQuery;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(1443))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(0.6f))
            .Set(new Component(2.25f))
            .Set(new Movement(Vector2.Zero))
            .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        // Make the ball roll around
        world.System<Model, Component, Movement, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Movement movement, ref Position position, ref Rotation rotation) =>
            {
                try
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
                                vfxSpawn.SpawnActorVfx("vfx/pop/m0318/eff/m0318_pop01h.avfx", model.GameObject, model.GameObject);
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

                    var entity = it.Entity(i);
                    // Fixed timestep simulation
                    movement.SimulationBufferTime += it.DeltaTime();
                    FixedUpdate(entity, ref movement, ref position);

                    entity.Set(new ModelTimelineSpeed(movement.Speed / MaxSpeed * AnimationSpeed));

                    // Rotate
                    var rotationSpeed = 4.0f;
                    var targetRotation = MathUtilities.VectorToRotation(movement.Direction);
                    var r = MathUtilities.ClampRadians(targetRotation - rotation.Value);
                    var rd1 = MathF.Abs(r);
                    var rd2 = 2 * MathF.PI - rd1;
                    var delta = rotationSpeed * it.DeltaTime();
                    if (rd1 < rd2)
                    {
                        if (rd1 < delta)
                        {
                            rotation.Value = targetRotation;
                        }
                        else
                        {
                            rotation.Value = MathUtilities.ClampRadians(rotation.Value + Math.Sign(r) * delta);
                        }
                    }
                    else
                    {
                        if (rd2 < delta)
                        {
                            rotation.Value = targetRotation;
                        }
                        else
                        {
                            rotation.Value = MathUtilities.ClampRadians(rotation.Value - Math.Sign(r) * delta);
                        }
                    }

                    // Hit detection
                    component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);

                    var player = dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }
                    if (component.Cooldown > 0) { return; }
                    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= HitboxRadius)
                    {
                        component.Cooldown = HitCooldown;

                        var knockbackDirection = player.Position - position.Value;
                        knockbackDirection.Y = 0;
                        if (knockbackDirection.LengthSquared() == 0)
                        {
                            var randomAngle = (float)(random.NextDouble() * 2 * Math.PI);
                            knockbackDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                        }

                        this.playerQuery.Each((Entity e, ref Player.Component pc) =>
                        {
                            Knockback.ApplyToPlayer(e, knockbackDirection, KnockbackDuration, true);
                        });
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });

        world.System<Component, ShowOmen, Position>()
            .Each((Iter it, int i, ref Component component, ref ShowOmen omen, ref Position position) =>
            {
                if (component.TimeUntilRolling > 0) { return; }

                if (!omen.Omen.IsValid())
                {
                    omen.Omen = CircleOmen.CreateEntity(it.World())
                        .ChildOf(it.Entity(i));
                }

                omen.Omen
                    .Set(new Position(position.Value))
                    .Set(new Scale(HitboxRadius * Vector3.One));
            });
    }

    private void FixedUpdate(Entity entity, ref Movement movement, ref Position position)
    {
        // Movement must be simulated in fixed update steps to maintain consistency across different client FPS values
        while (movement.SimulationBufferTime >= FixedDeltaTime)
        {
            movement.SimulationBufferTime -= FixedDeltaTime;

            var acceleration = 25.0f;
            var speed = movement.Speed;
            speed = Math.Clamp(speed + acceleration * (float)FixedDeltaTime, 0, MaxSpeed);
            var velocity = speed * Vector3.Normalize(movement.Direction.ToVector3(0));
            position.Value += velocity * (float)FixedDeltaTime;

            movement.Speed = speed;

            // Collision
            Vector2? reflectionNormal = null;
            if (entity.Has<CircleArena>())
            {
                var circleArena = entity.Get<CircleArena>();
                var p = position.Value.ToVector2();
                var normal = Vector2.Normalize(circleArena.Center - p);
                if (Vector2.Distance(circleArena.Center, p) > circleArena.Radius)
                {
                    reflectionNormal = normal;
                }
            }
            else if (entity.Has<SquareArena>())
            {
                var squareArena = entity.Get<SquareArena>();
                var p = position.Value.ToVector2();
                // -X
                if (p.X < squareArena.Center.X - squareArena.Width / 2f)
                {
                    reflectionNormal = new Vector2(1, 0);
                }
                // +X
                else if (p.X > squareArena.Center.X + squareArena.Width / 2f)
                {
                    reflectionNormal = new Vector2(-1, 0);
                }
                // -Y
                else if (p.Y < squareArena.Center.Y - squareArena.Width / 2f)
                {
                    reflectionNormal = new Vector2(0, 1);
                }
                // +Y
                else if (p.Y > squareArena.Center.Y + squareArena.Width / 2f)
                {
                    reflectionNormal = new Vector2(0, -1);
                }
            }

            if (reflectionNormal.HasValue &&
                Vector2.Dot(movement.Direction, reflectionNormal.Value) < 0)
            {
                // Ball outside arena, reflect it back in
                Random rand = entity.Has<SeededRandom>() ? entity.Get<SeededRandom>().Random : random;
                var newDirection = Vector2.Reflect(movement.Direction, reflectionNormal.Value);
                var angleVariance = rand.NextSingle() * float.DegreesToRadians(ReflectAngleVariance / 2f);
                angleVariance *= rand.Next(2) == 0 ? 1 : -1;
                newDirection = MathUtilities.Rotate(newDirection, angleVariance);
                movement.Direction = newDirection;
                movement.Speed = 0;
            }
        }
    }
}
