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

public sealed class Twister(DalamudServices dalamud, CommonQueries commonQueries, VfxSpawn vfxSpawn, Random random, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float Cooldown);

    private const float Radius = 0.9f;
    private const float KnockbackDuration = 5.0f;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("bgcommon/world/common/vfx_for_btl/b0222/eff/b0222_twis_y.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.System<Component, Position>()
            .Each((Iter it, int i, ref Component component, ref Position position) =>
            {
                try
                {
                    component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);

                    if (component.Cooldown > 0) { return; }

                    var player = dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= Radius)
                    {
                        component.Cooldown = 3.0f;

                        vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c0m.avfx", player, player);
                        vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c1m.avfx", player, player);
                        vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c2m.avfx", player, player);

                        var knockbackDirection = player.Position - position.Value;
                        knockbackDirection.Y = 0;
                        if (knockbackDirection.LengthSquared() == 0)
                        {
                            var randomAngle = (float)(random.NextDouble() * 2 * Math.PI);
                            knockbackDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                        }
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                        {
                            Knockback.ApplyToTarget(e, knockbackDirection, KnockbackDuration, false);
                        });
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }
}
