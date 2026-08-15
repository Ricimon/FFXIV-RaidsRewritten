using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace RaidsRewritten.Scripts.Systems;

public unsafe class PlayerModelSystem(Configuration configuration) : ISystem
{
    private readonly Configuration configuration = configuration;

    private const float zScale = 0.1f;

    public void Register(World world)
    {
        world.System<Flattened.Component>()
            .With<Player.Component>().Up()
            .Each((e, ref status) =>
            {
                var playerEntity = e.Parent();
                if (!playerEntity.TryGet<Player.Component>(out var player)) { return; }
                if (player.PlayerCharacter == null) { return; }
                var pPlayerGameObject = (GameObject*)player.PlayerCharacter.Address;
                if (pPlayerGameObject == null) { return; }
                var pPlayerDrawObject = pPlayerGameObject->DrawObject;
                if (pPlayerDrawObject == null) { return; }

                if (configuration.EverythingDisabled)
                {
                    if (status.OriginalSet)
                    {
                        pPlayerDrawObject->Scale.Z = status.OriginalZ;
                        pPlayerDrawObject->Rotation = status.OriginalRotation;
                    }
                    return;
                }

                if (pPlayerDrawObject->Scale.Z != zScale)
                {
                    status.OriginalSet = true;
                    status.OriginalZ = pPlayerDrawObject->Scale.Z;
                    pPlayerDrawObject->Scale.Z = zScale;
                }

                // check if player model is facing up, make it if not
                var playerModelFacing = Vector3.Transform(new Vector3(0, 0, 1), pPlayerDrawObject->Rotation);
                float alignment = Vector3.Dot(playerModelFacing, new Vector3(0, 1, 0));
                if (alignment < 0.99f)
                {
                    status.OriginalSet = true;
                    status.OriginalRotation = pPlayerDrawObject->Rotation;
                    var maths = Quaternion.CreateFromAxisAngle(new Vector3(-1, 0, 0), MathF.PI / 2);
                    pPlayerDrawObject->Rotation = Quaternion.Normalize(pPlayerDrawObject->Rotation * maths);
                }
            });

        world.Observer<Flattened.Component>()
            .With<Player.Component>().Up()
            .Event(Ecs.OnRemove)
            .Each((e, ref status) =>
            {
                var playerEntity = e.Parent();
                if (!playerEntity.TryGet<Player.Component>(out var player)) { return; }
                if (player.PlayerCharacter == null) { return; }
                var pPlayerGameObject = (GameObject*)player.PlayerCharacter.Address;
                if (pPlayerGameObject == null) { return; }
                var pPlayerDrawObject = pPlayerGameObject->DrawObject;
                if (pPlayerDrawObject == null) { return; }

                if (configuration.EverythingDisabled)
                {
                    if (status.OriginalSet)
                    {
                        pPlayerDrawObject->Scale.Z = status.OriginalZ;
                        pPlayerDrawObject->Rotation = status.OriginalRotation;
                    }
                    return;
                }

                playerEntity.Set(new Flattened.FallingOff(status.OriginalZ, status.OriginalRotation));
                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/toad_smk0f.avfx"))
                    .Set(new Scale(new System.Numerics.Vector3(1.5f)))
                    .ChildOf(playerEntity);

            });

        world.System<Flattened.FallingOff, Player.Component>()
            .Each((Iter it, int i, ref Flattened.FallingOff component, ref Player.Component player) =>
            {
                component.ElapsedTime -= it.DeltaTime();

                if (component.ElapsedTime > 0 && !configuration.EverythingDisabled) { return; }
                var e = it.Entity(i);
                if (player.PlayerCharacter == null)
                {
                    e.Remove<Flattened.FallingOff>();
                    return;
                }
                var pPlayerGameObject = (GameObject*)player.PlayerCharacter.Address;
                if (pPlayerGameObject == null)
                {
                    e.Remove<Flattened.FallingOff>();
                    return;
                }
                var pPlayerDrawObject = pPlayerGameObject->DrawObject;
                if (pPlayerDrawObject == null)
                {
                    e.Remove<Flattened.FallingOff>();
                    return;
                }

                pPlayerDrawObject->Scale.Z = component.OriginalZ;
                pPlayerDrawObject->Rotation = component.OriginalRotation;

                e.Remove<Flattened.FallingOff>();
            });
    }
}
