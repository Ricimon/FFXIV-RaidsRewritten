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
            .Each((Iter it, int i, ref Vfx vfx, ref Position position, ref Rotation rotation, ref Scale scale) =>
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

                // Vfx self-destructed, because it finished playing
                if (vfx.VfxPtr.Vfx == null)
                {
                    it.Entity(i).Destruct();
                }
            });

        world.Observer<Vfx>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Vfx _) =>
            {
                // For whatever reason the ref Vfx variable does not match that of the entity variable
                // Probably some quirk of the Observer binding
                var vfx = e.Get<Vfx>();
                if (vfx.VfxPtr != null && vfx.VfxPtr.Vfx != null)
                {
                    if (e.Has<Omen>())
                    {
                        e.CsWorld().Entity()
                            .Set(new VfxFadeOut(vfx.VfxPtr, 0.25f, 0.25f));
                    }
                    else
                    {
                        e.CsWorld().Entity()
                            .Set(new VfxFadeOut(vfx.VfxPtr, 1.0f, 1.0f));
                    }
                }
            });

        world.System<VfxFadeOut>()
            .Each((Iter it, int i, ref VfxFadeOut vfxFade) =>
            {
                vfxFade.TimeRemaining -= it.DeltaTime();
                if (vfxFade.TimeRemaining > 0)
                {
                    vfxFade.VfxPtr.UpdateAlpha(vfxFade.TimeRemaining / vfxFade.Duration);
                }
                else
                {
                    vfxFade.VfxPtr.Remove();
                    it.Entity(i).Destruct();
                }
            });
    }
}
