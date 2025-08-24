using Dalamud.Game.ClientState.Objects.Types;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class DistanceSnapshotTether(CommonQueries commonQueries) : IAttack, ISystem
{

    public enum TetherConditionVfxTarget
    {
        Both,
        OnlySource,
        OnlyTarget
    }

    public record struct Tether(Action<Entity> StatusOnCondition);
    public record struct VfxOnFail(List<string> Vfx, TetherConditionVfxTarget VfxTarget = TetherConditionVfxTarget.Both);
    public record struct VfxOnSuccess(List<string> Vfx, TetherConditionVfxTarget VfxTarget = TetherConditionVfxTarget.Both);
    public record struct FailWhenFurtherThan(float distance);
    public record struct FailWhenCloserThan(float distance);
    public struct Activated;

    public Entity Create(World world)
    {
        return world.Entity()
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Tether, ActorVfxSource, ActorVfxTarget>().With<Activated>()
            .Each((Iter it, int i, ref Tether tether, ref ActorVfxSource sourceComponent, ref ActorVfxTarget targetComponent) =>
            {
                var entity = it.Entity(i);

                var source = sourceComponent.Source;
                var target = targetComponent.Target;

                if (ValidActor(source) && ValidActor(target))
                {
                    var sourcePos = new Vector2(source!.Position.X, source.Position.Z);
                    var targetPos = new Vector2(target!.Position.X, target.Position.Z);

                    var distance = Vector2.Distance(sourcePos, targetPos);

                    bool failed = false;

                    if (entity.TryGet<FailWhenFurtherThan>(out var far))
                    {
                        failed |= distance > far.distance;
                    }

                    if (entity.TryGet<FailWhenCloserThan>(out var close))
                    {
                        failed |= distance < close.distance;
                    }

                    if (failed)
                    {
                        // anonymous functions hates refs from outside
                        var onCondition = tether.StatusOnCondition;
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                        {
                            onCondition(e);
                        });

                        if (entity.TryGet<VfxOnFail>(out var vfxOnFail))
                        {
                            RunConditionResolveVfx(entity, source, target, vfxOnFail.Vfx, vfxOnFail.VfxTarget);
                        }
                    } else
                    {
                        if (entity.TryGet<VfxOnSuccess>(out var vfxOnSuccess))
                        {
                            RunConditionResolveVfx(entity, source, target, vfxOnSuccess.Vfx, vfxOnSuccess.VfxTarget);
                        }
                    }
                }

                entity.Destruct();
            });
    }

    public static Entity SetTetherVfx(Entity entity, TetherOmen.TetherVfx tetherVfx) => entity.Set(new ActorVfx(TetherOmen.TetherVfxes[tetherVfx]));

    private static bool ValidActor(IGameObject? gameObject) => gameObject != null && gameObject.IsValid();

    private static void RunConditionResolveVfx(Entity entity, IGameObject source, IGameObject target, List<string> vfxList, TetherConditionVfxTarget vfxTarget)
    {
        if (vfxTarget == TetherConditionVfxTarget.Both || vfxTarget == TetherConditionVfxTarget.OnlySource)
        {
            foreach (var vfx in vfxList)
            {
                entity.CsWorld().Entity().Set(new ActorVfxSource(source))
                    .Set(new ActorVfx(vfx));
            }
        }
        if (vfxTarget == TetherConditionVfxTarget.Both || vfxTarget == TetherConditionVfxTarget.OnlyTarget)
        {
            foreach (var vfx in vfxList)
            {
                entity.CsWorld().Entity().Set(new ActorVfxSource(target))
                .Set(new ActorVfx(vfx));
            }
        }
    }
}
