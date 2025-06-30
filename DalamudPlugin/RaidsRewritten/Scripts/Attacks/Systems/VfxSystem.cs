using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts.Attacks.Systems;

public unsafe class VfxSystem(VfxSpawn vfxSpawn, ILogger logger) : ISystem
{
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    public void Register(World world)
    {
        world.System<Vfx, Position, Rotation, Scale>()
            .Each((ref Vfx vfx, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                if (vfx.VfxPtr == null)
                {
                    vfx.VfxPtr = this.vfxSpawn.SpawnStaticVfx(vfx.Path, position.Value, rotation.Value);
                    if (scale.Value != default)
                    {
                        vfx.VfxPtr.UpdateScale(scale.Value);
                    }
                    else
                    {
                        scale.Value = vfx.VfxPtr.Vfx->Scale;
                    }
                }
            });

        world.Observer<Vfx>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Vfx _) =>
            {
                e.Get<Vfx>().VfxPtr?.Remove();
            });
    }
}
