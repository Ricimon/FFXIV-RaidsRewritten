using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Systems;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts.Attacks;

public class Twister(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float Cooldown);

    private readonly DalamudServices dalamud = dalamud;
    private readonly VfxSpawn vfxSpawn = vfxSpawn;
    private readonly ILogger logger = logger;

    private const float Radius = 0.9f;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Vfx("bgcommon/world/common/vfx_for_btl/b0222/eff/b0222_twis_y.avfx"))
            .Set(new Transform())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Transform>()
            .Each((Iter it, int i, ref Component component, ref Transform transform) =>
            {
                component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);

                if (component.Cooldown > 0) { return; }

                var player = this.dalamud.ClientState.LocalPlayer;
                if (player == null) { return; }

                if (Vector2.Distance(transform.Position.ToVector2(), player.Position.ToVector2()) < Radius)
                {
                    this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c0m.avfx", player, player);
                    this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c1m.avfx", player, player);
                    this.vfxSpawn.SpawnActorVfx("vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c2m.avfx", player, player);
                    component.Cooldown = 3.0f;
                }
            });
    }
}
