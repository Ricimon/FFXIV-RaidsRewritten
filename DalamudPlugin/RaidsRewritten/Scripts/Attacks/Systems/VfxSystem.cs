using Flecs.NET.Core;
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
        world.System<Vfx, Transform>()
            .Each((ref Vfx vfx, ref Transform transform) =>
            {
                if (vfx.VfxPtr == null)
                {
                    vfx.VfxPtr = this.vfxSpawn.SpawnStaticVfx(vfx.Path, transform.Position, transform.Rotation);
                    if (transform.Scale != default)
                    {
                        vfx.VfxPtr.UpdateScale(transform.Scale);
                    }
                    else
                    {
                        transform.Scale = vfx.VfxPtr.Vfx->Scale;
                    }
                }
                else
                {
                    //vfx.VfxPtr.UpdatePosition(transform.Position);
                    //vfx.VfxPtr.UpdateRotation(new(0, 0, transform.Rotation));
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
