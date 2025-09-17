using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class RepellingCannonADS (DalamudServices dalamud, VfxSpawn vfxSpawn, CommonQueries commonQueries) : IAttack, ISystem
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
        { Phase.Animation, 1.7f },
        { Phase.Omen, 2f },
        { Phase.Snapshot, 2.5f },
        { Phase.Vfx, 2.5f },
        { Phase.Reset, 6f },
    };

    public record struct Component(float ElapsedTime, Phase Phase = Phase.Animation);

    private const ushort IdleAnimation = 34;
    private const ushort AttackAnimation = 2256;
    private const float AttackScale = 10f;
    private const int HysteriaId = 0xF1B1;
    private const float HysteriaDuration = 30f;
    private const float RedirectInterval = 15f;
    private const string AttackVfx1 = "vfx/monster/m0105/eff/m0105sp_03t0m.avfx";
    private const string AttackVfx2 = "vfx/monster/m0105/eff/m0105sp_03t1m.avfx";

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(321))
            .Set(new AnimationState(1)) // ADS glow
            .Set(new Position())
            .Set(new Rotation(0))
            .Set(new Scale())
            .Set(new UniformScale(0.75f))
            .Set(new TimelineBase(IdleAnimation))
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    public void Register(World world)
    {
        world.System<Component, Position>()
            .Each((Iter it, int i, ref Component component, ref Position position) =>
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
                        CircleOmen.CreateEntity(world)
                            .Set(new Position(position.Value))
                            .Set(new Scale(new Vector3(AttackScale)))
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
                            if (player != null && !player.IsDead) {
                                if (CircleOmen.IsInOmen(child, player.Position))
                                {
                                    if (player.HasTranscendance())
                                    {
                                        DelayedAction.Create(world, () =>
                                        {
                                            vfxSpawn.PlayInvulnerabilityEffect(player);
                                        }, 0.5f);
                                    } else
                                    {
                                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                        {
                                            DelayedAction.Create(world, () =>
                                            {
                                                Hysteria.ApplyToTarget(e, HysteriaDuration, RedirectInterval, HysteriaId);
                                            }, 0.5f);
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
                        AddActorVfx(entity, AttackVfx1);
                        AddActorVfx(entity, AttackVfx2);
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
