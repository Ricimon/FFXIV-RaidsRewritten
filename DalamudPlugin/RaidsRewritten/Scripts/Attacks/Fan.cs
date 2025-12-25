using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public sealed class Fan(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : IEntity, ISystem
{
    public record struct Component(Action<Entity> OnHit, int Degrees);

    private readonly DalamudServices dalamud = dalamud;
    private readonly ILogger logger = logger;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                try
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null && !player.IsDead)
                    {
                        var distanceToBoss = Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2());
                        var bossPosition = position.Value.ToVector2();
                        var playerPosition = player.Position.ToVector2();
                        var rotationAngle = new Vector2(bossPosition.X + MathF.Sin(rotation.Value), bossPosition.Y + MathF.Cos(rotation.Value));
                        var angle = MathUtilities.GetAngleBetweenLines(bossPosition, playerPosition, bossPosition, rotationAngle);
                        //logger.Debug($"Omen angle: {MathHelper.RadToDeg(rotation.Value)}");
                        //logger.Debug($"Boss: {bossPosition}\nPlayer: {playerPosition}\nFacing: {rotationAngle}");
                        //logger.Debug($"Angle between player and facing: {MathHelper.RadToDeg(angle)}");

                        // C# doesn't like refs in anonymous functions
                        var onHit = component.OnHit;

                        if (distanceToBoss < scale.Value.Z && (angle <= MathHelper.DegToRad(component.Degrees / 2) || float.IsNaN(angle)))
                        {
                            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                            {
                                onHit(e);
                            });
                        }
                    }

                    it.Entity(i).Destruct();
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
