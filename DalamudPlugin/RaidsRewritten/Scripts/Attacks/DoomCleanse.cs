using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameFunctions;
using ECommons.Logging;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using static Flecs.NET.Core.Ecs.Units.Datas;

namespace RaidsRewritten.Scripts.Attacks;

public sealed class DoomCleanse(DalamudServices dalamud, ILogger logger) : IAttack, ISystem, IDisposable
{
    public record struct Component(IGameObject GameObject);

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
        DelayedAction.Create(world, () => e.Destruct(), 4);
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
                    if (c.GameObject != null && c.GameObject.IsValid())
                    {
                        unsafe
                        {
                            var csGameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)c.GameObject.Address;

                            //THIS IS A FILLER FOR csGameObject->EventState (see below)
                            var bytes = DumpGameObjectBytes(csGameObject);
                            if (bytes[112] == 0x07)
                            {
                                it.Entity(i).Destruct();
                            }

                            /*
                            if (csGameObject ->EventState == 0x07) //EventState is off by 4 bytes on the remapping
                            {
                                logger.Info("taken");
                                it.Entity(i).Destruct();
                            }
                            */
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }

    public unsafe static byte[] DumpGameObjectBytes(GameObject* obj)
    {
        if (obj == null)
            return Array.Empty<byte>();

        int size = sizeof(GameObject); // base struct size (0x1A0 currently)
        byte[] buffer = new byte[size];

        // Copy unmanaged memory into managed byte array
        Marshal.Copy((IntPtr)obj, buffer, 0, size);

        return buffer;
    }
}
