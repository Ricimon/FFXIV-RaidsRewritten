using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using SocketIO.Serializer.Core;
using static Flecs.NET.Core.Ecs.Units;

namespace RaidsRewritten.Scripts.Attacks;

public unsafe sealed class LastWish(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem, IDisposable
{   
    public record struct Component(HitZone Zone, bool IsTelegraph = false);

    private float OMEN_VISIBLE_SECONDS = 2.0f;
    private float STATUS_DELAY_SECONDS = 0.0f;
    private float DELAY_SECONDS = 0.0f;
    private static Vector3 ARENA_CENTER = new Vector3(100f, 0f, 100f);
    private int ATTACK_ID = 56;
    private float HEAVY_TIME = 1.0f;
    private string HIT_VFX = "vfx/monster/gimmick/eff/bahamut_wyvn_uchiage_c0m.avfx";
    private Query<Player.Component> playerQuery;
    public readonly DalamudServices dalamud = dalamud;
    public readonly ILogger logger = logger;
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public enum HitZone
    {
        In,
        Out,
        North,
        East,
        South,
        West
    }
    public enum ZoneType
    { 
        Rect,
        Donut,
        Circle
    }
    private struct ZoneData
    {
        public Vector3 Scale;
        public string OmenPath;
        public string PredictOmenPath;
        public string VfxPath;
        public Vector3 Position;
        public float VfxDelaySeconds;
        public ZoneType ZoneType;
        public float Rotation;
    }
    private static readonly Dictionary<HitZone, ZoneData> ZoneDict = new Dictionary<HitZone, ZoneData>
    {
        {
            HitZone.In, new ZoneData
            {
                Scale = 8 * Vector3.One,
                Position = ARENA_CENTER,
                OmenPath = "vfx/omen/eff/m0830_circle_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                ZoneType = ZoneType.Circle,
                Rotation = 0.0f,
            }
        },
        {
            HitZone.Out, new ZoneData
            {
                Scale = 25 * Vector3.One,
                Position = ARENA_CENTER,
                //OmenPath = "vfx/omen/eff/ytc2b3_dnt_omen0p.avfx",
                OmenPath = "vfx/omen/eff/m0830_donut9_2_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                Rotation = 0.0f,
                ZoneType = ZoneType.Donut,
            }
        },
        {
            HitZone.North, new ZoneData
            {
                Scale = 20 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(-20f, 0f, -10f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI / 180) * 90.0f,
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.East, new ZoneData
            {
                Scale = 20 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(10f, 0f, -20f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                Rotation = 0.0f,
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.South, new ZoneData
            {
                Scale = 20 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(20f, 0f, 10f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI / 180) * 270.0f,
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.West, new ZoneData
            {
                Scale = 20 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(-10f, 0f, 20f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI / 180) * 180.0f,
                ZoneType = ZoneType.Rect,
            }
        },
    };    
    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        world.System<Component, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                try
                {
                    var entity = it.Entity(i);
                    
                    if (component.IsTelegraph)
                    {
                        if (ZoneDict.TryGetValue(component.Zone, out var ZoneData))
                        {
                            world.Entity()
                                .Set(new StaticVfx(ZoneData.OmenPath))
                                .Set(new Position(ZoneData.Position))
                                .Set(new Rotation(ZoneData.Rotation))
                                .Set(new Scale(ZoneData.Scale))
                                .Add<Attack>()
                                .Add<Omen>()
                                .ChildOf(entity);
                            entity.Remove<Component>();
                            DelayedAction.Create(world, () => { entity.Destruct(); }, OMEN_VISIBLE_SECONDS);
                        }
                    }
                    else
                    {
                        var player = dalamud.ClientState.LocalPlayer;
                        if (player == null || player.IsDead) { return; }
                        
                        if (ZoneDict.TryGetValue(component.Zone, out var ZoneData))
                        {
                            bool hit = false;
                            switch (ZoneData.ZoneType)
                            {
                                case ZoneType.Circle:
                                    if (Vector2.Distance(ARENA_CENTER.ToVector2(), player.Position.ToVector2()) <= ZoneData.Scale.X)
                                    {
                                        hit = true;
                                    }
                                    break;
                                case ZoneType.Donut:
                                    if (Vector2.Distance(ARENA_CENTER.ToVector2(), player.Position.ToVector2()) >= (ZoneData.Scale.X / 9 * 2))
                                    {
                                        hit = true;
                                    }
                                    break;
                                case ZoneType.Rect:
                                    logger.Info(player.Position.ToString());
                                    switch (component.Zone)
                                    {
                                        case HitZone.North:
                                            if (player.Position.Z <= ARENA_CENTER.Z)
                                            {
                                                hit = true;
                                            }
                                            break;
                                        case HitZone.East:
                                            if (player.Position.X >= ARENA_CENTER.X)
                                            {
                                                hit = true;
                                            }
                                            break;
                                        case HitZone.South:
                                            if (player.Position.Z >= ARENA_CENTER.Z)
                                            {
                                                hit = true;
                                            }
                                            break;
                                        case HitZone.West:
                                            if (player.Position.X <= ARENA_CENTER.X)
                                            {
                                                hit = true;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            if (hit)
                            {
                                vfxSpawn.SpawnActorVfx(HIT_VFX, player, player);
                                this.playerQuery.Each((Entity e, ref Player.Component pc) =>
                                {
                                    Heavy.ApplyToPlayer(e, HEAVY_TIME, ATTACK_ID, true);
                                });
                            }
                            entity.Destruct();
                        }
                    }
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
