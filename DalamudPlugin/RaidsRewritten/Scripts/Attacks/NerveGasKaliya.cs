using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using System.Collections.Generic;
using System.Numerics;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class NerveGasKaliya(DalamudServices dalamud, VfxSpawn vfxSpawn, CommonQueries commonQueries) : IAttack, ISystem
{
    public enum Phase
    {
        Animation,
        Omen,
        Snapshot,
        Vfx,
        Reset
    }

    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Animation, 1.9f },
        { Phase.Omen, 2f },
        { Phase.Snapshot, 2.75f },
        { Phase.Vfx, 2.75f },
        { Phase.Reset, 6f },
    };

    public record struct Component(float ElapsedTime, Phase Phase = Phase.Animation);

    private const float OmenDuration = 0.75f;
    private const ushort IdleAnimation = 34;
    private const ushort AttackAnimation = 3212;
    private const float AttackScale = 44f;
    private const float HysteriaDuration = 30f;
    private const float RedirectInterval = 15f;
    private const string AttackVfx = "vfx/monster/m0070/eff/m0070sp12c0h.avfx";

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(822))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new TimelineBase(IdleAnimation))
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.System<Component, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);

                switch (component.Phase)
                {
                    case Phase.Animation:
                        if (ShouldReturn(component)) { return; }
                        entity.Set(new TimelineBase(AttackAnimation));
                        component.Phase = Phase.Omen;
                        break;
                    case Phase.Omen:
                        if (ShouldReturn(component)) { return; }
                        Fan120Omen.CreateEntity(world)
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(new Vector3(AttackScale)))
                            .Set(new OmenDuration(OmenDuration, false))
                            .ChildOf(entity);
                        component.Phase = Phase.Snapshot;
                        break;
                    case Phase.Snapshot:
                        if (ShouldReturn(component)) { return; }

                        entity.Set(new TimelineBase(IdleAnimation));

                        entity.Children((Entity child) =>
                        {
                            if (!child.Has<Omen>()) { return; }

                            var player = dalamud.ClientState.LocalPlayer;
                            if (player != null && !player.IsDead)
                            {
                                if (Fan120Omen.IsInOmen(child, player.Position))
                                {
                                    if (player.HasTranscendance())
                                    {
                                        DelayedAction.Create(world, () =>
                                        {
                                            vfxSpawn.PlayInvulnerabilityEffect(player);
                                        }, 0.25f);
                                    } else
                                    {
                                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                        {
                                            DelayedAction.Create(world, () =>
                                            {
                                                Hysteria.ApplyToTarget(e, HysteriaDuration, RedirectInterval);
                                            }, 0.25f);
                                        });
                                    }
                                }
                            }
                            child.Destruct();
                        });

                        component.Phase = Phase.Vfx;
                        break;
                    case Phase.Vfx:
                        if (ShouldReturn(component)) { return; }
                        AddActorVfx(entity, AttackVfx);
                        component.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (ShouldReturn(component)) { return; }
                        entity.Destruct();
                        break;
                }
            });
    }

    private bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase];

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }
}
