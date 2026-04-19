using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Attacks;

/// <summary>
/// Visual model entity for Garuda, used in UWU debug simulations.
/// ModelChara rows 292–296 all use monster model m0087 (Garuda's model folder),
/// with variants 1–5 for different fight appearances:
/// Garuda - 303
/// Ifrit - 304
/// Leviathan - 305
/// Titan - 306
/// Ultima - 307-310
/// Shiva - 312
/// Omega - 327
/// Ramuh - 345
/// </summary>
public class Garuda : IEntity
{
    // ModelChara 292: monster m0087 (Garuda), Variant 1 — BNpcBase 2376, BNpcName 1644 "Garuda".
    // Try 293/294/295/296 for alternate variants (Extreme, UWU woken, etc.).
    private const int ModelCharaId = 303;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(ModelCharaId))
            .Set(new Position())
            .Set(new Rotation(0f))
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new TimelineBase(0))
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);
}
