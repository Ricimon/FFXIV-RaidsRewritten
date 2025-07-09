using System;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public unsafe class RollingBall(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float TimeUntilRolling, bool EntryAnimationPlayed = false);

    private readonly DalamudServices dalamud = dalamud;
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(1443))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(0.6f))
            .Set(new Component(2.0f))
            .Add<Attack>();
    }

    public void Register(World world)
    {
        // Make the ball roll around
        world.System<Model, Component, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                if (!model.Spawned) { return; }

                if (!component.EntryAnimationPlayed)
                {
                    component.EntryAnimationPlayed = true;
                    if (model.GameObject != null)
                    {
                        this.vfxSpawn.SpawnActorVfx("vfx/pop/m0318/eff/m0318_pop01h.avfx", model.GameObject, model.GameObject);
                    }
                }

                if (component.TimeUntilRolling > 0)
                {
                    component.TimeUntilRolling = Math.Max(component.TimeUntilRolling - it.DeltaTime(), 0);
                }

                // The rolling animation takes a little time to startup
                if (component.TimeUntilRolling < 0.1f)
                {
                    var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
                    var chara = (Character*)obj;
                    if (chara != null)
                    {
                        chara->Timeline.BaseOverride = 41;
                    }
                }

                if (component.TimeUntilRolling > 0) { return; }

                position.Value += MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0) * 5 * it.DeltaTime();
            });
    }
}
