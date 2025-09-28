using Flecs.NET.Core;
using Dalamud.Game.ClientState.Objects.Types;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using System.Numerics;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Systems;

public unsafe class VfxSystem(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : ISystem
{
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    public void Register(World world)
    {
        // Static VFX
        world.System<StaticVfx, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref StaticVfx vfx, ref Position position, ref Rotation rotation, ref Scale scale) =>
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
                    return;
                }

                if (it.Changed())
                {
                    vfx.VfxPtr.UpdatePosition(position.Value);
                    vfx.VfxPtr.UpdateRotation(new Vector3(0, 0, rotation.Value));
                    vfx.VfxPtr.UpdateScale(scale.Value);
                    vfx.VfxPtr.Update();
                }
            });

        world.System<StaticVfx, Alpha>()
            .Each((Iter it, int i, ref StaticVfx vfx, ref Alpha alpha) =>
            {
                if (!it.Changed()) { return; }
                if (vfx.VfxPtr == null) { return; }
                vfx.VfxPtr.UpdateAlpha(alpha.Value);
            });

        world.Observer<StaticVfx>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref StaticVfx _) =>
            {
                // For whatever reason the ref Vfx variable does not match that of the entity variable
                // Probably some quirk of the Observer binding
                var vfx = e.Get<StaticVfx>();
                if (vfx.VfxPtr != null && vfx.VfxPtr.Vfx != null)
                {
                    float a = 1.0f;
                    if (e.TryGet(out Alpha alpha))
                    {
                        a = alpha.Value;
                        if (a == 0)
                        {
                            vfx.VfxPtr.Remove();
                            return;
                        }
                    }

                    Entity fadeOutEntity;
                    if (e.Has<Omen>())
                    {
                        // Finish the fade
                        var duration = 0.25f * a / 1.0f;
                        fadeOutEntity = e.CsWorld().Entity()
                            .Set(new VfxFadeOut(vfx.VfxPtr, duration, duration));
                    }
                    else
                    {
                        // Non-omen VFX may be intentionally transparent, so use the full fadeout duration
                        fadeOutEntity = e.CsWorld().Entity()
                            .Set(new VfxFadeOut(vfx.VfxPtr, 1.0f, 1.0f));
                    }

                    if (a != 1.0f)
                    {
                        fadeOutEntity.Set(new Alpha(a));
                    }
                }
            });

        // Actor VFX
        world.System<Model, ActorVfx>()
            .TermAt(0).Self().Up()
            .Each((Iter it, int i, ref Model model, ref ActorVfx vfx) =>
            {
                ProcessActorVfx(it.Entity(i), model.GameObject, ref vfx);
            });

        world.System<ActorVfxSource, ActorVfx>()
            .Without<Model>()
            .Each((Iter it, int i, ref ActorVfxSource source, ref ActorVfx vfx) =>
            {
                ProcessActorVfx(it.Entity(i), source.Source, ref vfx);
            });

        world.System<Player.Component, ActorVfx>()
            .TermAt(0).Up()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Player.Component pc, ref ActorVfx vfx) =>
            {
                var localPlayer = dalamud.ClientState.LocalPlayer;
                if (vfx.VfxPtr == null && localPlayer != null)
                {
                    vfx.VfxPtr = vfxSpawn.SpawnActorVfx(vfx.Path, localPlayer, localPlayer);
                }

                // Vfx self-destructed, because it finished playing
                if (vfx.VfxPtr != null && vfx.VfxPtr.Vfx == null)
                {
                    it.Entity(i).Destruct();
                    return;
                }
            });

        world.System<ActorVfx, Scale>()
            .Each((Iter it, int i, ref ActorVfx vfx, ref Scale scale) =>
            {
                if (!it.Changed()) { return; }
                vfx.VfxPtr?.UpdateScale(scale.Value);
            });

        world.Observer<ActorVfx>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref ActorVfx _) =>
            {
                var vfx = e.Get<ActorVfx>();
                // UpdateAlpha doesn't seem to work for actor vfxes either.
                // Just removing it for now
                if (vfx.VfxPtr != null && vfx.VfxPtr.Vfx != null)
                {
                    vfx.VfxPtr.Remove();
                }
            });

        // Common
        world.System<VfxFadeOut>()
            .Each((Iter it, int i, ref VfxFadeOut vfxFade) =>
            {
                var entity = it.Entity(i);
                vfxFade.TimeRemaining -= it.DeltaTime();

                float a = 1.0f;
                if (entity.TryGet(out Alpha alpha))
                {
                    a = alpha.Value;
                }

                if (vfxFade.TimeRemaining > 0 && a > 0)
                {
                    vfxFade.VfxPtr.UpdateAlpha(a * vfxFade.TimeRemaining / vfxFade.Duration);
                }
                else
                {
                    vfxFade.VfxPtr.Remove();
                    it.Entity(i).Destruct();
                }
            });
    }

    private static bool ActorVfxShouldDestruct(ActorVfx actorVfx, Entity e)
    {
        if (actorVfx.VfxPtr == null) { return false; }
        if (actorVfx.VfxPtr.Vfx == null) { return true; }

        if (e.TryGet<ActorVfxTarget>(out var target))
        {
            if (target.Target != null && !target.Target.IsValid())
            {
                return true;
            }
        }

        return false;
    }

    private void ProcessActorVfx(Entity entity, IGameObject? source, ref ActorVfx vfx)
    {
        // UpdateScale doesn't seem to work for actor vfxes from a quick test. Should be looked into
        // Position/Rotation should be based on source actor
        if (vfx.VfxPtr == null && source != null)
        {
            if (entity.TryGet<ActorVfxTarget>(out var targetComponent))
            {
                var target = targetComponent.Target;
                if (target != null && target.IsValid())
                {
                    vfx.VfxPtr = vfxSpawn.SpawnActorVfx(vfx.Path, source, target);
                } else
                {
                    // don't bother spawning vfx if target isn't valid
                    entity.Destruct();
                    return;
                }
            } else
            {
                vfx.VfxPtr = vfxSpawn.SpawnActorVfx(vfx.Path, source, source);
            }
        }

        // Vfx self-destructed, because it finished playing or target is gone
        if (ActorVfxShouldDestruct(vfx, entity))
        {
            entity.Destruct();
            return;
        }
    }
}
