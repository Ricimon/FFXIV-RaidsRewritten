using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class Tornado (DalamudServices dalamud, Random random, CommonQueries commonQueries, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float ElapsedTime, float CooldownTime);

    private float TimeTillActive = 2f;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(2199))
            .Set(new Component())
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(1f))  // 3y
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.System<Component, Position, UniformScale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref UniformScale uniformScale) =>
            {
                component.ElapsedTime += it.DeltaTime();
                if (component.ElapsedTime < TimeTillActive) { return; }
                if (component.ElapsedTime < component.CooldownTime) { return; }

                var player = dalamud.ClientState.LocalPlayer;
                if (player == null) { return; }

                var playerPosV2 = MathHelper.ToVector2(player.Position);
                var tornadoPosV2 = MathHelper.ToVector2(position.Value);

                if (Vector2.Distance(playerPosV2, tornadoPosV2) < uniformScale.Value * 3)
                {
                    var knockbackDirection = player.Position - position.Value;
                    knockbackDirection.Y = 0;
                    if (knockbackDirection.LengthSquared() == 0)
                    {
                        var randomAngle = (float)(random.NextDouble() * 2 * Math.PI);
                        knockbackDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                    }
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                    {
                        Knockback.ApplyToTarget(e, knockbackDirection, 10, false);
                    });

                    component.CooldownTime = component.ElapsedTime + 3f;
                }
            });
    }
}
