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

    public enum TetherVfxTarget
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
    public record struct Tether(Func<float, bool> Condition, Action<Entity> OnCondition1, Action OnCondition2, bool RunOnce = true);
    public record struct VfxOnCondition(string Vfx, TetherVfxTarget VfxTarget = TetherVfxTarget.Both);
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
                if (tether.RunOnce && entity.Has<Broken>()) { return; }

                var source = sourceComponent.Source;
                var target = targetComponent.Target;
                if (ValidActor(source) && ValidActor(target))
                {
                    var sourcePos = new Vector2(source!.Position.X, source.Position.Z);
                    var targetPos = new Vector2(target!.Position.X, target.Position.Z);

                    if (tether.Condition(Vector2.Distance(sourcePos, targetPos)))
                    {
                        // anonymous functions hates refs from outside
                        var onCondition = tether.OnCondition1;
                        this.playerQuery.Each((Entity e, ref Player.Component _) =>
                        {
                            onCondition(e);
                        });

                        tether.OnCondition2();

                        if (tether.RunOnce && entity.TryGet<VfxOnCondition>(out var vfxOnCondition))
                        {
                            if (vfxOnCondition.VfxTarget == TetherVfxTarget.Both || vfxOnCondition.VfxTarget == TetherVfxTarget.OnlySource)
                            {
                                world.Entity().Set(new ActorVfxSource(source))
                                    .Set(new ActorVfx(vfxOnCondition.Vfx)).ChildOf(entity);
                            }
                            if (vfxOnCondition.VfxTarget == TetherVfxTarget.Both || vfxOnCondition.VfxTarget == TetherVfxTarget.OnlyTarget)
                            {
                                world.Entity().Set(new ActorVfxSource(target))
                                    .Set(new ActorVfx(vfxOnCondition.Vfx)).ChildOf(entity);
                            }
                        }
                        entity.Add<Broken>();
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
}
