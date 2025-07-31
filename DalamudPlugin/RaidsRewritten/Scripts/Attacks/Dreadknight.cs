using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using NLog;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Data.Files.ScdFile;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class Dreadknight(DalamudServices dalamud) : IAttack, IDisposable, ISystem
{
    public record struct Component(float ElapsedTime, float NextRefresh, float Enrage = 10f, bool CanHit = false);

    private const float StunDuration = 8f;
    private const int StunId = 0xDEAD;
    private const float StunDelay = 0.5f;
    private const float EnrageDuration = 100f;
    private const float InitialDelay = 2f;

    private Query<Player.Component> playerQuery;

    public Entity Create(World world)
    {
        return world.Entity()
                .Set(new Model(379))
                .Set(new Rotation(0))
                .Set(new Scale())
                .Set(new UniformScale(1f))
                .Set(new Component())
                .Set(new Position())
                .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        world.System<Model, Component, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);


                if (entity.TryGet<ActorVfxTarget>(out var target))
                {
                    if (target.Target != null && target.Target.IsValid())
                    {
                        var fakeActorPos = new Vector2(position.Value.X, position.Value.Z);
                        var north = new Vector2(position.Value.X, position.Value.Z + 1);
                        var playerPos = new Vector2(target.Target.Position.X, target.Target.Position.Z);
                        var angle = MathUtilities.GetAngleBetweenLines(fakeActorPos, north, fakeActorPos, playerPos);
                        if (playerPos.X < fakeActorPos.X) { angle = -angle; }
                        if (float.IsNaN(angle)) angle = 0;
                        rotation.Value = angle;

                        if (Vector2.DistanceSquared(fakeActorPos, playerPos) > 2.5)
                        {
                            SetTimeline(model, 41);
                            var newPosition = position.Value;
                            newPosition.Z += 2.5f * it.DeltaTime() * MathF.Cos(angle);
                            newPosition.X += 2.5f * it.DeltaTime() * MathF.Sin(angle);
                            position.Value = newPosition;
                            component.CanHit = true;
                        } else
                        {
                            if (component.CanHit && component.ElapsedTime > component.NextRefresh)
                            {
                                SetTimeline(model, 1515);
                                StunPlayer(world, StunDuration);
                                component.NextRefresh = component.ElapsedTime + 3f;
                            } else
                            {
                                SetTimeline(model, 0);
                                if (component.ElapsedTime > InitialDelay)
                                {
                                    component.CanHit = true;
                                }
                            }
                        }

                        component.Enrage = component.ElapsedTime + 5f;
                    } else
                    {
                        // remove the actorvfx target
                    }
                } else
                {
                    if (component.ElapsedTime > component.Enrage)
                    {

                    }
                }

                if (component.ElapsedTime > 300)
                {
                    entity.Destruct();
                }
            });
    }

    private void SetTimeline(Model model, ushort animationId)
    {
        unsafe
        {
            var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
            var chara = (Character*)obj;
            if (chara != null)
            {
                chara->Timeline.BaseOverride = animationId;
            }
        }
    }

    private void StunPlayer(World world, float duration)
    {
        this.playerQuery.Each((Entity e, ref Player.Component _) =>
        {
            DelayedAction.Create(world, () => {
                Stun.ApplyToPlayer(e, duration, StunId);
            }, StunDelay);
        });
    }
}
