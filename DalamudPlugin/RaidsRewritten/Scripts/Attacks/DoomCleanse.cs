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
using RaidsRewritten.Utility;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ECommons.GameFunctions;
using RaidsRewritten.Memory;
using ECommons;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Attacks;

public sealed class DoomCleanse(DalamudServices dalamud, ILogger logger) : IAttack, ISystem, IDisposable
{
    public record struct Component(uint EntityId);

    private Query<Player.Component> playerQuery;
    private Query<Component> componentQuery;
    public Entity Create(World world)
    {
        var e =world.Entity()
            .Set(new StaticVfx("bgcommon/world/common/vfx_for_btl/b3566/eff/b3566_rset_y1.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(2*Vector3.One))
            .Set(new Component())
            .Add<Attack>();
        DelayedAction.Create(world, () => e.Destruct(), 2);
        return e;
    }
    public void Dispose()
    {
        this.playerQuery.Dispose();
        this.componentQuery.Dispose();
    }

    public void Register(Flecs.NET.Core.World world)
    {
        this.playerQuery = Player.QueryForLocalPlayer(world);
        this.componentQuery = world.QueryBuilder<Component>().Cached().Build();
        world.System<Component>()
            .Each((Iter it, int i, ref Component c) =>
            {
            try
            {
                uint EntityId = c.EntityId;
                var obj = dalamud.ObjectTable.FirstOrDefault(x => x.EntityId == EntityId);
                if (obj != null && obj.IsValid()) { return; }

                    Entity e = it.Entity(i);
                    e.Destruct();
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }    
}
