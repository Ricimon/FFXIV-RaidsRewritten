using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class TetherOmen : ISystem
{
    // Contained references for TOP Hello World / TEA Inception distance tethers
    public enum TetherVfx
    {
        ActivatedClose,
        ActivatedFar,
        DelayedClose,
        DelayedFar,
        TooClose,
        GoodDistance,
    }

    public static readonly Dictionary<TetherVfx, string> TetherVfxes = new()
    {
        {TetherVfx.ActivatedClose, "vfx/channeling/eff/chn_alpha0h.avfx"},
        {TetherVfx.ActivatedFar, "vfx/channeling/eff/chn_beta0h.avfx"},
        {TetherVfx.DelayedClose, "vfx/channeling/eff/chn_m0771_alpha0c.avfx"},
        {TetherVfx.DelayedFar, "vfx/channeling/eff/chn_m0771_beta0c.avfx"},
        {TetherVfx.TooClose, "vfx/channeling/eff/chn_arrow01f.avfx" },
        {TetherVfx.GoodDistance, "vfx/channeling/eff/chn_dark001f.avfx" },
    };

    // Logic for distance-reactive tethers
    public record struct ProximityTether(
        float DistanceThreshold,
        IGameObject? Source, IGameObject? Target,
        string TooCloseVfxPath = "", string GoodDistanceVfxPath = "",
        Entity TooCloseTether = default, Entity GoodDistanceTether = default);

    public void Register(World world)
    {
        world.System<ProximityTether>()
            .Each((Iter it, int index, ref ProximityTether pt) =>
            {
                var entity = it.Entity(index);
                if (pt.Source == null || !pt.Source.IsCompletelyValid())
                {
                    entity.Destruct();
                    return;
                }
                if (pt.Target == null || !pt.Target.IsCompletelyValid())
                {
                    entity.Destruct();
                    return;
                }

                if (string.IsNullOrEmpty(pt.TooCloseVfxPath))
                {
                    pt.TooCloseVfxPath = TetherVfxes[TetherVfx.TooClose];
                }
                if (string.IsNullOrEmpty(pt.GoodDistanceVfxPath))
                {
                    pt.GoodDistanceVfxPath = TetherVfxes[TetherVfx.GoodDistance];
                }

                var distanceToTarget = MathUtilities.Vector2Distance(pt.Source.Position, pt.Target.Position);
                if (distanceToTarget < pt.DistanceThreshold)
                {
                    pt.GoodDistanceTether.SafeDestruct();
                    if (!pt.TooCloseTether.IsValid())
                    {
                        pt.TooCloseTether = it.World().Entity()
                            .Set(new ActorVfx(pt.TooCloseVfxPath))
                            .Set(new ActorVfxTarget(pt.Target))
                            .ChildOf(entity);
                    }
                }
                else
                {
                    pt.TooCloseTether.SafeDestruct();
                    if (!pt.GoodDistanceTether.IsValid())
                    {
                        pt.GoodDistanceTether = it.World().Entity()
                            .Set(new ActorVfx(pt.GoodDistanceVfxPath))
                            .Set(new ActorVfxTarget(pt.Target))
                            .ChildOf(entity);
                    }
                }
            });
    }
}
