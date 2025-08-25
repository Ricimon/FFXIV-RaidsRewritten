using System;
using System.Linq;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Star(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : IAttack, ISystem
{
    public enum Phase
    {
        Omen,
        DamageDelay,
        Visual,
    }

    public record struct Component(
        float OmenTime,
        string VfxPath,
        Action<Entity> OnHit,
        float TimeElapsed = 0,
        Phase Phase = Phase.Omen,
        Entity OmenEntity = default);

    private const float DamageDelay = 0.5f;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(5.0f * Vector3.One))
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
                                component.OmenEntity = StarOmen.CreateEntity(it.World())
                                    .Set(new Position(position.Value))
                                    .Set(new Rotation(rotation.Value))
                                    .Set(new Scale(scale.Value))
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

        if (player != null && !player.IsDead &&
            // Transcendance, TODO: play invulnerable vfx
            !player.StatusList.Any(s => s.StatusId == GameConstants.TranscendanceStatusId))
        {
            // This attack and its omen share transform values, so this is okay
            if (StarOmen.IsInOmen(entity, player.Position))
            {
                var onHit = component.OnHit;
                if (onHit != null)
                {
                    DelayedAction.Create(entity.CsWorld(), () =>
                    {
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                        {
                            onHit(e);
                        });
                    }, DamageDelay).ChildOf(entity);
                }
            }
        }
    }
}
