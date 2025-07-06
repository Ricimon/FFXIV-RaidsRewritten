using Dalamud.Game.ClientState.Objects.Types;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class RollingBall(ILogger logger) : IAttack, ISystem
{
    public record struct Component(object _);

    private readonly ILogger logger = logger;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(1443))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(0.6f))
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        // Make the ball roll around
        world.System<Model, Component, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                if (model.Spawned)
                {
                    unsafe
                    {
                        var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
                        var chara = (Character*)obj;
                        if (chara != null)
                        {
                            chara->Timeline.BaseOverride = 41;
                        }
                    }

                    position.Value += MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(0) * 5 * it.DeltaTime();
                }
            });
    }
}
