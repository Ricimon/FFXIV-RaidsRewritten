using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class GearBeam(DalamudServices dalamud, Lazy<EntityManager> entityManager, CommonQueries commonQueries, VfxSpawn vfxSpawn) : IEntity, ISystem
{
    public record struct Component(float Radius, Entity Model = default);
    public record struct Beam(
        float OmenDuration,
        float ElapsedTime = 0,
        float AttackWindupReplayTimer = 0,
        float AttackCooldown = 0,
        Entity Omen = default,
        List<Entity>? AttackWindups = default,
        Entity Attack = default);

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(1.8f))
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                var entity = it.Entity(i);

                if (!component.Model.IsValid())
                {
                    if (entityManager.Value.TryCreateEntity<Gear>(out Entity gear))
                    {
                        gear
                            .Add<Attack>()
                            .ChildOf(entity);
                        component.Model = gear;

                        // Entry VFX
                        FakeActor.CreateInvalidModel(it.World())
                            .Set(new ActorVfx("vfx/pop/m0318/eff/m0318_pop01h.avfx"))
                            .Set(new Position(position.Value))
                            .ChildOf(gear);
                    }
                }

                if (component.Model.IsValid())
                {
                    var p = position.Value;
                    if (entity.TryGet(out FullRotation fullRotation))
                    {
                        p += Vector3.Transform(-2.0f * Vector3.UnitY, fullRotation.Value);
                    }
                    else
                    {
                        p += -2.0f * Vector3.UnitY;
                    }
                    component.Model
                        .Set(new Position(p))
                        .Set(new Rotation(rotation.Value))
                        .Set(new UniformScale(1.8f / 5.0f * component.Radius));
                }
            });

        world.System<Component, FullRotation>()
            .Each((Iter it, int i, ref Component component, ref FullRotation rotation) =>
            {
                if (!it.Changed()) { return; }
                if (component.Model.IsValid())
                {
                    component.Model.Set(new FullRotation(rotation.Value));
                }
            });

        world.System<Component, Beam, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Beam beam, ref Position position, ref Rotation rotation) =>
            {
                var entity = it.Entity(i);

                beam.ElapsedTime += it.DeltaTime();
                beam.AttackWindupReplayTimer = Math.Max(beam.AttackWindupReplayTimer - it.DeltaTime(), 0);
                beam.AttackCooldown = Math.Max(beam.AttackCooldown - it.DeltaTime(), 0);

                // Omen & Windup
                if (beam.ElapsedTime < beam.OmenDuration)
                {
                    if (!beam.Omen.IsValid() && entityManager.Value.TryCreateEntity<RectangleOmen>(out var rectOmen))
                    {
                        rectOmen
                            .Set(new Scale(new(3.5f, 1.0f, 50.0f)))
                            .Set(new OmenDuration(beam.OmenDuration, false))
                            .ChildOf(entity);
                        beam.Omen = rectOmen;
                    }
                    if (beam.AttackWindupReplayTimer == 0)
                    {
                        var attackWindup = FakeActor.CreateInvalidModel(it.World())
                            .Set(new ActorVfx("vfx/monster/d1085/eff/d1085_sp_011_c0v.avfx"))
                            .ChildOf(entity);
                        beam.AttackWindups ??= [];
                        beam.AttackWindups.Add(attackWindup);
                        beam.AttackWindupReplayTimer = 3.0f;
                    }
                }

                if (beam.Omen.IsValid())
                {
                    beam.Omen
                        .Set(new Position(position.Value))
                        .Set(new Rotation(rotation.Value));
                }
                if (beam.AttackWindups != null)
                {
                    foreach (var aw in beam.AttackWindups)
                    {
                        if (aw.IsValid())
                        {
                            aw
                                .Set(new Position(position.Value))
                                .Set(new Rotation(rotation.Value + MathF.PI));
                        }
                    }
                }

                // Attack
                if (beam.ElapsedTime >= beam.OmenDuration &&
                    !beam.Attack.IsValid())
                {
                    beam.Attack = FakeActor.CreateInvalidModel(it.World())
                        .Set(new ActorVfx("vfx/common/eff/d1085_idle_sp2_c0v.avfx"))
                        .ChildOf(entity);
                }

                if (beam.Attack.IsValid())
                {
                    var attackPosition = position.Value;
                    var d = MathUtilities.RotationToUnitVector(rotation.Value - 0.5f * MathF.PI).ToVector3(position.Value.Y);
                    attackPosition += 0.2f * d;
                    beam.Attack
                        .Set(new Position(attackPosition))
                        .Set(new Rotation(rotation.Value));

                    var player = dalamud.ObjectTable.LocalPlayer;
                    if (player == null || player.IsDead) { return; }
                    if (beam.AttackCooldown == 0 &&
                        beam.Omen.IsValid() && RectangleOmen.IsInOmen(beam.Omen, player.Position))
                    {
                        beam.AttackCooldown = 3.0f;
                        if (player.HasTranscendance())
                        {
                            vfxSpawn.PlayInvulnerabilityEffect(player);
                        }
                        else
                        {
                            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                            {
                                Pacify.ApplyToTarget(e, 60.0f);
                            });
                        }
                    }
                }
            });

        world.Observer<Beam>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Beam _) =>
            {
                var beam = e.Get<Beam>();
                beam.Omen.SafeDestruct();
                if (beam.AttackWindups != null)
                {
                    foreach (var aw in beam.AttackWindups)
                    {
                        aw.SafeDestruct();
                    }
                }
                beam.Attack.SafeDestruct();
            });
    }
}
