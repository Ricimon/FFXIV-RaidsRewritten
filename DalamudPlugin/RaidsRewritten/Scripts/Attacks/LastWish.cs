using System;
using System.Collections.Generic;
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

public unsafe sealed class LastWish(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : IAttack, ISystem, IDisposable
{   
    public record struct Component(HitZone Zone, bool IsTelegraph = false);

    public const float OMEN_VISIBLE_SECONDS = 1.5f;
    private float STATUS_DELAY_SECONDS = 0.0f;
    private float DELAY_SECONDS = 0.0f;
    private static Vector3 ARENA_CENTER = new Vector3(0f, 0f, 0f);
    //private static Vector3 ARENA_CENTER = new Vector3(100f, 0f, 100f);
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
    List<Entity> ToDestruct = [];
    public void Dispose()
    {
        this.playerQuery.Dispose();
        foreach (Entity e in ToDestruct)
        {
            e.Destruct();
        }
        ToDestruct.Clear();
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
        public string VfxPath;
        public Vector3 Position;
        public float VfxDelaySeconds;
        public float VfxRotation;
        public Vector3 VfxPosition;
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
                //VfxPath = "vfx/monster/gimmick4/eff/n5rb_b3_g04t0k1.avfx",
                VfxPath = "vfx/monster/gimmick5/eff/o6b1_ba03_g01c0g.avfx",
                VfxDelaySeconds = 0.0f,
                ZoneType = ZoneType.Circle,
                Rotation = 0.0f,
                VfxRotation = 0.0f,
                VfxPosition = ARENA_CENTER,
            }
        },
        {
            HitZone.Out, new ZoneData
            {
                Scale = 22.5f * Vector3.One,
                Position = ARENA_CENTER,
                //OmenPath = "vfx/omen/eff/ytc2b3_dnt_omen0p.avfx",
                OmenPath = "vfx/omen/eff/m0830_donut9_2_dark_01k1.avfx",
                //VfxPath = "vfx/monster/gimmick4/eff/n5rb_b3_g01t0k1.avfx",
                //VfxPath = "vfx/monster/m0676/eff/m676_sp017_c0p.avfx",
                VfxPath = "vfx/monster/gimmick5/eff/o6b1_ce06_gimmick02_c0x1.avfx",
                VfxDelaySeconds = 0.0f,
                Rotation = 0.0f,
                ZoneType = ZoneType.Donut,
                VfxRotation = 0.0f,
                VfxPosition = ARENA_CENTER,
            }
        },
        {
            HitZone.North, new ZoneData
            {
                Scale = 22 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(-22f, 0f, -11f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                VfxPath = "vfx/monster/gimmick4/eff/l5c6b10g01c1a1.avfx",
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI / 180) * 90.0f,
                ZoneType = ZoneType.Rect,
                VfxRotation = (float)Math.PI/180*270,
                VfxPosition = ARENA_CENTER,
            }
        },
        {
            HitZone.East, new ZoneData
            {
                Scale = 22 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(11f, 0f, -22f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                VfxPath = "vfx/monster/gimmick4/eff/l5c6b10g01c1a1.avfx",
                VfxDelaySeconds = 0.0f,
                Rotation = 0.0f,
                ZoneType = ZoneType.Rect,
                VfxRotation = (float)Math.PI,
                VfxPosition = ARENA_CENTER,
            }
        },
        {
            HitZone.South, new ZoneData
            {
                Scale = 22 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(22f, 0f, 11f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                VfxPath = "vfx/monster/gimmick4/eff/l5c6b10g01c0a1.avfx",
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI / 180) * 270.0f,
                ZoneType = ZoneType.Rect,
                VfxRotation = (float)Math.PI/180*270,
                VfxPosition = ARENA_CENTER,
            }
        },
        {
            HitZone.West, new ZoneData
            {
                Scale = 22 * new Vector3(.5f, 1, 2),
                Position = ARENA_CENTER + new Vector3(-11f, 0f, 22f),
                OmenPath = "vfx/omen/eff/m0830_laser_dark_01k1.avfx",
                VfxPath = "vfx/monster/gimmick4/eff/l5c6b10g01c0a1.avfx",
                //vfx/monster/m0830/eff/m0830sp02c1k1.avfx
                VfxDelaySeconds = 0.0f,
                Rotation = (float)(Math.PI),
                ZoneType = ZoneType.Rect,
                VfxRotation = (float)Math.PI,
                VfxPosition = ARENA_CENTER,
                            //n5rb
                            //chamber of the fourteen
                            //themis
                                //.Set(new StaticVfx(ZoneData.OmenPath))
            }
        },
    };
    public void HitCheck(HitZone z)
    {
        var player = dalamud.ClientState.LocalPlayer;
        if (player == null || player.IsDead) { return; }

        
        if (ZoneDict.TryGetValue(z, out var ZoneData))
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
                    switch (z)
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
        }
    }

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
                            var telegraph = world.Entity()
                                .Set(new StaticVfx(ZoneData.OmenPath))
                                .Set(new Position(ZoneData.Position))
                                .Set(new Rotation(ZoneData.Rotation))
                                .Set(new Scale(ZoneData.Scale))
                                .Add<Attack>()
                                .Add<Omen>()
                                .ChildOf(entity);
                            entity.Remove<Component>();

                            void DestroyTelegraph()
                            { 
                                ToDestruct.Remove(telegraph);
                                telegraph.Destruct();
                            }

                            var delayedAction = DelayedAction.Create(world, DestroyTelegraph, OMEN_VISIBLE_SECONDS);
                            ToDestruct.Add(delayedAction);
                        }
                    }
                    else
                    {
                        if (ZoneDict.TryGetValue(component.Zone, out var ZoneData))
                        {
                            var fakeActor = FakeActor.Create(world)
                                .Set(new Position(ZoneData.VfxPosition))
                                .Set(new Rotation(ZoneData.VfxRotation))
                                .Set(new Scale(new Vector3(1f)));
                            ToDestruct.Add(fakeActor);
                            fakeActor.Set(new ActorVfx(ZoneData.VfxPath));

                            HitZone zone = component.Zone;
                            var delayedAction = DelayedAction.Create(world, () => HitCheck(zone), ZoneData.VfxDelaySeconds);
                            ToDestruct.Add(delayedAction);
                        }
                        entity.Destruct();
                    }
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
