using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class Fan(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public record struct Component(object _);

    private readonly DalamudServices dalamud = dalamud;
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    private const int Degrees = 90;
    private const float StunDuration = 10.0f;
    public Entity Create(World world)
    {
        //logger.Debug("Hello");
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        //logger.Debug("Hello2");
        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                try
                {
                    var player = this.dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    var distanceToBoss = Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2());
                    var bossPosition = position.Value.ToVector2();
                    var playerPosition = player.Position.ToVector2();
                    var rotationAngle = new Vector2(bossPosition.X + MathF.Sin(rotation.Value), bossPosition.Y + MathF.Cos(rotation.Value));
                    var angle = MathHelper.GetAngleBetweenLines(bossPosition, playerPosition, bossPosition, rotationAngle);  // TODO: figure out why this is NaN sometimes
                    //logger.Debug($"Omen angle: {MathHelper.RadToDeg(rotation.Value)}");
                    //logger.Debug($"Angle between player and facing: {MathHelper.RadToDeg(angle)}");

                    if (distanceToBoss < scale.Value.Z && (angle <= MathHelper.DegToRad(Degrees / 2) || float.IsNaN(angle)))
                    {
                        Player.Query(it.World()).Each((Entity e, ref Player.Component pc) =>
                        {
                            // TODO: add delay for stun
                            Bound.ApplyToPlayer(e, StunDuration);
                        });
                    }

                    it.Entity(i).Destruct();

                } catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
        }
}
