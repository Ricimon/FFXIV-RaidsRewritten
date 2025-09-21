using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Star(DalamudServices dalamud, CommonQueries commonQueries, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public enum Type
    {
        Short,
        Long
    }

    public enum Phase
    {
        Omen,
        DamageDelay,
        Visual,
    }

    public record struct Component(
        Type Type,
        float OmenTime,
        string VfxPath,
        Action<Entity> OnHit,
        bool IgnoreTranscendance = false,
        float TimeElapsed = 0,
        Phase Phase = Phase.Omen,
        Entity OmenEntity = default);

    private const float DamageDelay = 0.25f;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(Vector3.One))
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                var entity = it.Entity(i);

                component.TimeElapsed += it.DeltaTime();

                switch (component.Phase)
                {
                    case Phase.Omen:
                        if (component.TimeElapsed < component.OmenTime)
                        {
                            // Create Omen
                            if (!component.OmenEntity.IsValid())
                            {
                                if (component.Type == Type.Short)
                                {
                                    component.OmenEntity = ShortStarOmen.CreateEntity(it.World())
                                        .Set(new Scale(ShortStarOmen.ScaleMultiplier * scale.Value));
                                }
                                else
                                {
                                    component.OmenEntity = LongStarOmen.CreateEntity(it.World())
                                        .Set(new Scale(LongStarOmen.ScaleMultiplier * scale.Value));
                                }
                                component.OmenEntity
                                    .Set(new Position(position.Value))
                                    .Set(new Rotation(rotation.Value))
                                    .Set(new OmenDuration(component.OmenTime, false))
                                    .ChildOf(entity);
                            }
                        }
                        else
                        {
                            component.Phase = Phase.DamageDelay;

                            if (component.OmenEntity.IsValid())
                            {
                                component.OmenEntity.Destruct();
                            }
                            Snapshot(entity, component);
                        }
                        break;

                    case Phase.DamageDelay:
                        if (component.TimeElapsed > component.OmenTime + DamageDelay)
                        {
                            component.Phase = Phase.Visual;

                            if (!string.IsNullOrEmpty(component.VfxPath))
                            {
                                for (var j = 1; j >= 0; j--)
                                {
                                    var fakeActor = FakeActor.Create(it.World())
                                        .Set(new Position(position.Value))
                                        .Set(new Rotation(rotation.Value + j * 0.25f * MathF.PI))
                                        .Set(new Scale(Vector3.One))
                                        .ChildOf(entity);
                                    fakeActor.Set(new ActorVfx(component.VfxPath));
                                }
                            }
                        }
                        break;

                    case Phase.Visual:
                        if (!entity.HasChildren() ||
                            component.TimeElapsed > component.OmenTime + DamageDelay + 3.0f)
                        {
                            entity.Destruct();
                        }
                        break;
                }
            });
    }

    private void Snapshot(Entity entity, Component component)
    {
        var player = dalamud.ClientState.LocalPlayer;

        if (player == null || player.IsDead) { return; }

        if (IsInAttack(entity, player.Position))
        {
            var onHit = component.OnHit;
            if (onHit != null)
            {
                if (player.HasTranscendance() && !component.IgnoreTranscendance)
                {
                    DelayedAction.Create(entity.CsWorld(), () =>
                    {
                        vfxSpawn.PlayInvulnerabilityEffect(player);
                    }, DamageDelay).ChildOf(entity);
                }
                else
                {
                    DelayedAction.Create(entity.CsWorld(), () =>
                    {
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                        {
                            onHit(e);
                        });
                    }, DamageDelay, true).ChildOf(entity);
                }
            }
        }
    }

    private static bool IsInAttack(Entity attack, Vector3 position)
    {
        if (!attack.TryGet<Position>(out var p)) { return false; }
        if (!attack.TryGet<Rotation>(out var r)) { return false; }
        if (!attack.TryGet<Scale>(out var s)) { return false; }

        var width = 4 * s.Value.X;

        bool inAttack = false;

        for (var i = 0; i < 8; i++)
        {
            var rotation = r.Value + i * 0.25f * MathF.PI;
            var forward = MathUtilities.RotationToUnitVector(rotation);
            var right = MathUtilities.RotationToUnitVector(rotation - 0.5f * MathF.PI);

            var originToPosition = position.ToVector2() - p.Value.ToVector2();
            var amountForward = Vector2.Dot(forward, originToPosition);
            var amountRight = Vector2.Dot(right, originToPosition);

            if (amountForward >= 0 &&
                amountRight >= -0.5f * width &&
                amountRight <= 0.5f * width)
            {
                inAttack = true;
                break;
            }
        }

        return inAttack;
    }
}
