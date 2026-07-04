using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RaidsRewritten.Scripts.Attacks;

public class FireTornadoEntity (DalamudServices dalamud, VfxSpawn vfxSpawn, CommonQueries commonQueries) : IEntity, ISystem
{
    public class Donut (DalamudServices dalamud, VfxSpawn vfxSpawn, CommonQueries commonQueries) : ISystem
    {
        public enum Phase
        {
            Omen,
            Animation,
            Snapshot,
            Vfx,
            Reset
        }
        public record struct Component(float ElapsedTime, Phase Phase = Phase.Omen);
        private const ushort AttackAnimation = 7594;
        private const float StunDuration = 10f;
        private const float AttackDelay = 0f;
        private const string AttackVfx = "vfx/monster/m0935/eff/m0935_sp11_c0p.avfx";
        private const float OmenDuration = 2f;

        private static readonly Dictionary<Phase, float> phaseTimings = new()
        {
            { Phase.Omen, 0f },
            { Phase.Animation, 1.65f },
            { Phase.Snapshot, 1.65f },
            { Phase.Vfx, 1.5f },
            { Phase.Reset, 6f },
        };

        public static Entity CreateEntity(World world)
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
            world.System<Component, Position>().Each((Iter it, int i, ref Component donut, ref Position position) =>
            {
                var e = it.Entity(i);
                donut.ElapsedTime += it.DeltaTime();
                var parent = e.Parent();
                if (!parent.IsValid()) { return; }

                switch(donut.Phase)
                {
                    case Phase.Omen:
                        if (ShouldReturn(donut)) { return; }
                        ConflagDonut.CreateEntity(world)
                            .Set(new Position(position.Value))
                            .Set(new OmenDuration(OmenDuration, false))
                            .ChildOf(e);
                        donut.Phase = Phase.Vfx;
                        break;
                    case Phase.Vfx:
                        if (ShouldReturn(donut)) { return; }

                        AddActorVfx(e, AttackVfx);

                        donut.Phase = Phase.Animation;
                        break;
                    case Phase.Animation:
                        if (ShouldReturn(donut)) { return; }
                        parent.Set(new TimelineBase(AttackAnimation));
                        donut.Phase = Phase.Snapshot;
                        break;
                    case Phase.Snapshot:
                        if (ShouldReturn(donut)) { return; }

                        parent.Set(new TimelineBase(IdleAnimation));

                        e.Children((Entity child) =>
                        {
                            if (!child.Has<Omen>()) { return; }

                            var player = dalamud.ObjectTable.LocalPlayer;
                            if (player != null && !player.IsDead)
                            {
                                if (ConflagDonut.IsInOmen(child, player.Position))
                                {
                                    if (player.HasTranscendance())
                                    {
                                        DelayedAction.Create(world, () => vfxSpawn.PlayInvulnerabilityEffect(player), 0.2f).ChildOf(e);
                                    }
                                    else
                                    {
                                        commonQueries.LocalPlayerQuery.Each((Entity player, ref Player.Component _) =>
                                        {
                                            DelayedAction.Create(world, () => Stun.ApplyToTarget(player, StunDuration), 0.2f).ChildOf(player);
                                        });
                                    }
                                }
                            }
                            child.Destruct();
                        });

                        donut.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (ShouldReturn(donut)) { return; }
                        e.Destruct();
                        break;
                }
            });
        }

        private static bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase] + AttackDelay;
    }

    public class Cone(DalamudServices dalamud, VfxSpawn vfxSpawn, CommonQueries commonQueries, ILogger logger) : ISystem
    {
        public enum Phase
        {
            Omen,
            Animation,
            Snapshot,
            Vfx,
            Reset
        }

        public record struct Component(float ElapsedTime, Phase Phase = Phase.Omen);
        private const ushort AttackAnimation = 7594;
        private const float StunDuration = 10f;
        private const float AttackDelay = 0f;
        private const float OmenDuration = 2f;
        private const string AttackVfx = "vfx/monster/gimmick2/eff/z3oe_b3_g05c0i.avfx";

        private static readonly Dictionary<Phase, float> phaseTimings = new()
        {
            { Phase.Omen, 0f },
            { Phase.Animation, 1.35f },
            { Phase.Snapshot, 1.35f },
            { Phase.Vfx, 2.25f },
            { Phase.Reset, 6f },
        };

        public static Entity CreateEntity(World world)
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
            world.System<Component, Position, Rotation>().Each((Iter it, int i, ref Component donut, ref Position position, ref Rotation rotation) =>
            {
                var e = it.Entity(i);
                donut.ElapsedTime += it.DeltaTime();
                var parent = e.Parent();
                if (!parent.IsValid()) { return; }

                switch (donut.Phase)
                {
                    case Phase.Omen:
                        if (ShouldReturn(donut)) { return; }
                        Fan15Omen.CreateEntity(world)
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(new Vector3(40)))
                            .Set(new OmenDuration(OmenDuration, false))
                            .ChildOf(e);
                        donut.Phase = Phase.Animation;
                        break;
                    case Phase.Animation:
                        if (ShouldReturn(donut)) { return; }
                        parent.Set(new TimelineBase(AttackAnimation));
                        donut.Phase = Phase.Snapshot;
                        break;
                    case Phase.Snapshot:
                        if (ShouldReturn(donut)) { return; }

                        parent.Set(new TimelineBase(IdleAnimation));

                        e.Children((Entity child) =>
                        {
                            if (!child.Has<Omen>()) { return; }

                            var player = dalamud.ObjectTable.LocalPlayer;
                            if (player != null && !player.IsDead)
                            {
                                if (Fan15Omen.IsInOmen(child, player.Position))
                                {
                                    if (player.HasTranscendance())
                                    {
                                        DelayedAction.Create(world, () => vfxSpawn.PlayInvulnerabilityEffect(player), 0.2f).ChildOf(e);
                                    }
                                    else
                                    {
                                        commonQueries.LocalPlayerQuery.Each((Entity player, ref Player.Component _) =>
                                        {
                                            DelayedAction.Create(world, () => Stun.ApplyToTarget(player, StunDuration), 0.2f).ChildOf(player);
                                        });
                                    }
                                }
                            }
                            child.Destruct();
                        });

                        donut.Phase = Phase.Vfx;
                        break;
                    case Phase.Vfx:
                        if (ShouldReturn(donut)) { return; }

                        var fakeActor = FakeActor.Create(it.World())
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .ChildOf(e);

                        AddActorVfx(fakeActor, AttackVfx);

                        donut.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (ShouldReturn(donut)) { return; }
                        e.Destruct();
                        break;
                }
            });
        }

        private static bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase] + AttackDelay;
    }
    public record struct Component(float TimeElapsed = 0f);

    private const ushort IdleAnimation = 8;
    private const float Tick = 1.5f;
    private const float PuddleRadius = 5f;
    private const float PunishmentDuration = 5f;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(1666))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(2.5f))
            .Set(new TimelineBase(IdleAnimation))
            .Set(new StaticVfx("bgcommon/world/common/vfx_for_btl/b0997/eff/b0997_yuka_o.avfx"))
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }

    public static void DonutMech(Entity entity)
    {
        var hasPosition = entity.TryGet<Position>(out var position);
        if (!hasPosition) { return; }
        Donut.CreateEntity(entity.CsWorld())
            .Set(new Position(position.Value))
            .ChildOf(entity);
    }

    public static void ProteanMech(Entity entity, Vector3 targetPosition)
    {
        var hasPosition = entity.TryGet<Position>(out var position);
        if (!hasPosition) { return; }
        var rotation = MathUtilities.GetAbsoluteAngleFromSourceToTarget(position.Value, targetPosition);
        Cone.CreateEntity(entity.CsWorld())
            .Set(new Position(position.Value))
            .Set(new Rotation(rotation))
            .ChildOf(entity);
    }

    public void Register(World world)
    {
        world.System<Component, Position>().Each((Iter it, int i, ref Component component, ref Position position) =>
        {
            component.TimeElapsed += it.DeltaTime();
            if (component.TimeElapsed < Tick) { return; }
            
            component.TimeElapsed = 0;

            var p = dalamud.ObjectTable.LocalPlayer;
            if (p == null || !p.IsValid()) { return; }

            var centerV2 = p.Position.ToVector2();
            var positionV2 = position.Value.ToVector2();
            var distance = Vector2.Distance(centerV2, positionV2);

            if (distance < PuddleRadius)
            {
                if (p.HasTranscendance())
                {
                    DelayedAction.Create(world, () => vfxSpawn.PlayInvulnerabilityEffect(p), 0.2f).ChildOf(it.Entity(i));
                }
                else
                {
                    commonQueries.LocalPlayerQuery.Each((Entity player, ref Player.Component _) =>
                    {
                        // maybe this should be a pacify instead?
                        DelayedAction.Create(world, () => Stun.ApplyToTarget(player, PunishmentDuration, overrideExistingDuration: true), 0.2f).ChildOf(player);
                    });
                }
            }
            });
    }
}
