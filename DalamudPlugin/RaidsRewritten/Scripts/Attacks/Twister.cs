using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts.Attacks;

public class Twister(DalamudServices dalamud, VfxSpawn vfxSpawn, Random random, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float Cooldown);

    private readonly DalamudServices dalamud = dalamud;
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    private const float Radius = 0.9f;
    private const float KnockbackDuration = 2.0f;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Vfx("bgcommon/world/common/vfx_for_btl/b0222/eff/b0222_twis_y.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        var playerQuery = world.QueryBuilder<Player.Component>().Cached().Build();

        world.System<Component, Position>()
            .Each((Iter it, int i, ref Component component, ref Position position) =>
            {
                try
                {
                    component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);

                    if (component.Cooldown > 0) { return; }

                    var player = this.dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) < Radius)
                    {
                        this.logger.Info("Knocking back");
                        component.Cooldown = 3.0f;

                        this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c0m.avfx", player, player);
                        this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c1m.avfx", player, player);
                        this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c2m.avfx", player, player);

                        var knockbackDirection = player.Position - position.Value;
                        knockbackDirection.Y = 0;
                        if (knockbackDirection.LengthSquared() == 0)
                        {
                            var randomAngle = (float)(random.NextDouble() * 2 * Math.PI);
                            knockbackDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                        }
                        Player.Query(it.World()).Each((Entity e, ref Player.Component pc) =>
                        {
                            KnockedBack.ApplyToPlayer(e, knockbackDirection, KnockbackDuration);
                        });
                    }
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
