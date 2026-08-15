using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.IO;

namespace RaidsRewritten.Scripts.Systems;

public unsafe class StatusSystem(
    CommonQueries commonQueries,
    Configuration configuration,
    DalamudServices dalamud,
    StatusCommonProcessor statusCommonProcessor,
    ILogger logger) : ISystem
{
    private const float zScale = 0.1f;
    private readonly CommonQueries commonQueries = commonQueries;
    private readonly Configuration configuration = configuration;
    private readonly DalamudServices dalamud = dalamud;
    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly ILogger logger = logger;

    public void Register(World world)
    {
        world.Observer<Condition.Status>()
            .With<Condition.StatusEnhancement>()
            .With<Player.Component>().Up()
            .Event(Ecs.OnSet)
            .Each((e, ref status) => HandleApplyStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusEnfeeblement>()
            .With<Player.Component>().Up()
            .Event(Ecs.OnSet)
            .Each((e, ref status) => HandleApplyStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusOther>()
            .With<Player.Component>().Up()
            .Event(Ecs.OnSet)
            .Each((e, ref status) => HandleApplyStatus(e, status));

        // Avoid multiple flytext entities on a single status entity
        world.System<Condition.Status>()
            .Each((Entity e, ref Condition.Status _) =>
            {
                var flytextFound = false;
                e.Children(Ecs.DependsOn, child =>
                {
                    // FlyTextReady needs to be consumed first before entity destruction
                    if (child.Has<FlyText>() && !child.Has<FlyTextReady>())
                    {
                        if (flytextFound)
                        {
                            child.Destruct();
                        }
                        flytextFound = true;
                    }
                });
            });

        world.System<FlyText>()
            .Each((Entity e, ref FlyText flytext) =>
            {
                // handles status fall off flytext
                if (e.Target(Ecs.DependsOn).IsValid()) { return; }
                var charaEntityId = flytext.OwnerEntityId;
                var dChara = dalamud.ObjectTable.SearchByEntityId(charaEntityId);
                if (dChara == null) { return; }
                var chara = (Character*)dChara.Address;
                if (chara == null || !chara->IsCharacter())
                {
                    e.Destruct();
                    return; 
                }
                if (e.Has<FlyTextReady>()) { return; }
                e.Set(new FlyTextReady(new(flytext.Status, false)));
            });

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

    private void HandleApplyStatus(Entity statusEntity, Condition.Status status)
    {
        if (!configuration.EverythingDisabled && !configuration.UseLegacyStatusRendering)
        {
            // handle extended statuses
            statusEntity.Children(Ecs.DependsOn, (Entity child) =>
            {
                child.Destruct();
            });

            commonQueries.AllPlayersQuery.Each((Entity pEntity, ref Player.Component player) =>
            {
                if (!statusEntity.IsChildOf(pEntity)) { return; }
                var dChara = player.PlayerCharacter;
                if (dChara == null || !dChara.IsValid()) { return; }
                var chara = (Character*)dChara.Address;
                if (!chara->IsCharacter()) { return; }

                DelayedAction.Create(statusEntity.CsWorld(), () =>
                {
                    if (!statusEntity.IsValid()) { return; }
                    var isEnfeeblement = statusEntity.Has<Condition.StatusEnfeeblement>();

                    var flytext = statusEntity.CsWorld().Entity()
                        .Set(new FlyText(statusEntity, status, isEnfeeblement, chara->EntityId))
                        .Set(new FlyTextReady(new(status, true)))
                        .Add(Ecs.DependsOn, statusEntity);

                    if (statusEntity.TryGet<Condition.StatusIconReplacement>(out var r))
                    {
                        // DefaultTextureScale 1 == low res, 2 == high res
                        var hr = IsUsingHighResTextures() ? "_hr1" : "";
                        var replacementPath = Path.Combine("statuses", $"{r.CustomIconName}{hr}.tex");
                        replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
                        // The FileReplacement's original file path must be in a different folder than that of the icon to replace
                        var folder = r.IconToReplace - r.IconToReplace % 1000 - 1000;
                        var fr = new FileReplacement($"ui/icon/{folder:D6}/{r.IconToReplace}{hr}.tex", replacementPath);
                        flytext.Set(fr);
                        statusEntity.Set(new FileReplacementReference(fr));
                    }
                }, 0, true);
            });
        }
    }

    // DefaultTextureScale 1 == low res, 2 == high res
    private bool IsUsingHighResTextures() => RaptureAtkModule.Instance()->AtkModule.AtkTextureResourceManager.DefaultTextureScale == 2;
}
