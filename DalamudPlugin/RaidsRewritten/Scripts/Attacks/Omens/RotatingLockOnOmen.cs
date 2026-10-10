using Dalamud.Game.ClientState.Objects.Types;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class RotatingLockOnOmen(DalamudServices dalamud) : IEntity, ISystem
{
    public const float OmenDuration = 6.0f;

    public enum Direction
    {
        Clockwise,
        Counterclockwise,
    }
    public record struct Component(Direction Direction, IGameObject Target, Entity? LockOn = null, Entity? Rotation = null);

    private const string LockOnClockwiseVfxPath = "vfx/lockon/eff/lockon6_t0t.avfx";
    private const string LockOnCounterclockwiseVfxPath = "vfx/lockon/eff/m0126trg_t2h.avfx";
    private const string RotationClockwiseVfxPath = "vfx/lockon/eff/x6r3_turning_right_c0e1.avfx";
    private const string RotationCounterclockwiseVfxPath = "vfx/lockon/eff/x6r3_turning_left_c0e1.avfx";
    private const string RotationClockwiseVfxPathReplacement = "vfx/lockon_turning_right.avfx";
    private const string RotationCounterclockwiseVfxPathReplacement = "vfx/lockon_turning_left.avfx";

    public Entity Create(World world)
    {
        var omenContainer = world.Entity()
            .Set(new Component())
            .Add<Attack>()
            .Add<Omen>();

        return omenContainer;
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                var entity = it.Entity(i);

                if (component.LockOn == null)
                {
                    var vfxPath = component.Direction == Direction.Clockwise ? LockOnClockwiseVfxPath : LockOnCounterclockwiseVfxPath;

                    var lockOnVfx = world.Entity()
                        .Set(new ActorVfx(vfxPath))
                        .Set(new ActorVfxSource(component.Target))
                        .Add<Attack>()
                        .Add<Omen>()
                        .ChildOf(entity);

                    if (component.Direction == Direction.Counterclockwise)
                    {
                        lockOnVfx.Set(new ExpectFileReplacement());
                        var replacementPath = dalamud.PluginInterface.GetResourcePath("vfx/lockon_blue.avfx");
                        it.World().Entity()
                            .Set(new FileReplacement(vfxPath, replacementPath))
                            .ChildOf(lockOnVfx);
                    }

                    component.LockOn = lockOnVfx;
                }

                if (component.Rotation == null)
                {
                    var vfxPath = component.Direction == Direction.Clockwise ? RotationClockwiseVfxPath : RotationCounterclockwiseVfxPath;
                    var vfxPathReplacement = component.Direction == Direction.Clockwise ? RotationClockwiseVfxPathReplacement : RotationCounterclockwiseVfxPathReplacement;

                    var rotationVfx = it.World().Entity()
                        .Set(new ActorVfx(vfxPath))
                        .Set(new ActorVfxSource(component.Target))
                        .Set(new ExpectFileReplacement())
                        .Add<Attack>()
                        .Add<Omen>()
                        .ChildOf(entity);

                    var replacementPath = dalamud.PluginInterface.GetResourcePath(vfxPathReplacement);
                    it.World().Entity()
                        .Set(new FileReplacement(vfxPath, replacementPath))
                        .ChildOf(rotationVfx);

                    component.Rotation = rotationVfx;
                }

                if (!component.LockOn.Value.IsValid() && !component.Rotation.Value.IsValid())
                {
                    entity.Destruct();
                }
            });
    }
}
