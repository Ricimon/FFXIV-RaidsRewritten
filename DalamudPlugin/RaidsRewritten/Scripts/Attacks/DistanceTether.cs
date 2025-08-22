using Dalamud.Game.ClientState.Objects.Types;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static RaidsRewritten.Scripts.Attacks.DistanceTether;

namespace RaidsRewritten.Scripts.Attacks;

public class DistanceTether : IAttack, ISystem, IDisposable
{
    public enum TetherVfx
    {
        ActivatedClose,
        ActivatedFar,
        DelayedClose,
        DelayedFar,
    }

    public enum TetherConditionVfxTarget
    {
        Both,
        OnlySource,
        OnlyTarget
    }

    public static readonly Dictionary<TetherVfx, string> TetherVfxes = new()
    {
        {TetherVfx.ActivatedClose, "vfx/channeling/eff/chn_alpha0h.avfx"},
        {TetherVfx.ActivatedFar, "vfx/channeling/eff/chn_beta0h.avfx"},
        {TetherVfx.DelayedClose, "vfx/channeling/eff/chn_m0771_alpha0c.avfx"},
        {TetherVfx.DelayedFar, "vfx/channeling/eff/chn_m0771_beta0c.avfx"},
    };

    // Condition: bool func(float distance)
    // RunOnce: only checks condition a single time
    // ProcConditionOnce: check condition until condition is fulfilled once
    public record struct Tether(Func<float, bool> Condition, Action<Entity> StatusOnCondition, Action OnCondition, bool RunOnce = false, bool ProcConditionOnce = true);
    // vfx when condition is fulfilled
    public record struct VfxOnCondition(List<string> Vfx, TetherConditionVfxTarget VfxTarget = TetherConditionVfxTarget.Both);
    // vfx when condition isn't fulfilled and DestructTetherVfxAfterActive is true
    // for tethers that resolve once instead of continuously e.g house arrest/restraining order vs mid/far glitch
    public record struct VfxOnResolve(List<string> Vfx, TetherConditionVfxTarget VfxTarget = TetherConditionVfxTarget.Both);
    public struct Activated;
    public struct Broken;

    private Query<Player.Component> playerQuery;

    public Entity Create(World world)
    {
        return world.Entity()
            .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.QueryForLocalPlayer(world);

        world.System<Tether, ActorVfxSource, ActorVfxTarget>().With<Activated>()
            .Each((Iter it, int i, ref Tether tether, ref ActorVfxSource sourceComponent, ref ActorVfxTarget targetComponent) =>
            {
                var entity = it.Entity(i);
                if (tether.ProcConditionOnce && entity.Has<Broken>()) { return; }

                var source = sourceComponent.Source;
                var target = targetComponent.Target;

                if (ValidActor(source) && ValidActor(target))
                {
                    var sourcePos = new Vector2(source!.Position.X, source.Position.Z);
                    var targetPos = new Vector2(target!.Position.X, target.Position.Z);

                    if (tether.Condition(Vector2.Distance(sourcePos, targetPos)))
                    {
                        // anonymous functions hates refs from outside
                        var onCondition = tether.StatusOnCondition;
                        this.playerQuery.Each((Entity e, ref Player.Component _) =>
                        {
                            onCondition(e);
                        });

                        tether.OnCondition();

                        if (tether.RunOnce && entity.TryGet<VfxOnCondition>(out var vfxOnCondition))
                        {
                            RunConditionResolveVfx(entity, source, target, vfxOnCondition.Vfx, vfxOnCondition.VfxTarget);
                        }

                        entity.Add<Broken>();
                    } else
                    {
                        if (tether.RunOnce && entity.TryGet<VfxOnResolve>(out var vfxOnResolve))
                        {
                            RunConditionResolveVfx(entity, source, target, vfxOnResolve.Vfx, vfxOnResolve.VfxTarget);
                        }
                    }

                    if (tether.RunOnce)
                    {
                        entity.Remove<Activated>().Remove<ActorVfx>();
                    }
                }
            });
    }

    public static Entity SetTetherVfx(Entity entity, string tetherVfx)
    {
        RemoveTetherVfx(entity);
        // this can't be done on the same frame for w/e reason
        DelayedAction.Create(
            entity.CsWorld(),
            () => { entity.Set(new ActorVfx(tetherVfx)); },
            0);
        return entity;
    }

    public static bool IsBroken(Entity entity) => entity.Has<Broken>();
    public static Entity RemoveTetherVfx(Entity entity) => entity.Remove<ActorVfx>();
    private static bool ValidActor(IGameObject? gameObject) => gameObject != null && gameObject.IsValid();

    private static void RunConditionResolveVfx(Entity entity, IGameObject source, IGameObject target, List<string> vfxList, TetherConditionVfxTarget vfxTarget)
    {
        if (vfxTarget == TetherConditionVfxTarget.Both || vfxTarget == TetherConditionVfxTarget.OnlySource)
        {
            foreach (var vfx in vfxList)
            {
                entity.CsWorld().Entity().Set(new ActorVfxSource(source))
                    .Set(new ActorVfx(vfx)).ChildOf(entity);
            }
        }
        if (vfxTarget == TetherConditionVfxTarget.Both || vfxTarget == TetherConditionVfxTarget.OnlyTarget)
        {
            foreach (var vfx in vfxList)
            {
                entity.CsWorld().Entity().Set(new ActorVfxSource(target))
                .Set(new ActorVfx(vfx)).ChildOf(entity);
            }
        }
    }
}
