using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor,
    ILogger logger) : ISystem
{
    private readonly CommonQueries commonQueries = commonQueries;
    private readonly Configuration configuration = configuration;
    private readonly DalamudServices dalamud = dalamud;
    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor = statusFlyPopupTextProcessor;
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

        world.System<FlyText>()
            .Each((Entity e, ref FlyText flytext) =>
            {
                // handles status fall off flytext
                if (e.Target(Ecs.DependsOn).IsValid()) { return; }
                var chara = (Character*)StatusCommonProcessor.LocalPlayer();
                if (chara == null || !chara->IsCharacter())
                {
                    e.Destruct();
                    return; 
                }
                if (e.Has<FlyTextReady>()) { return; }
                e.Set(new FlyTextReady(new(flytext.Status, false, chara->EntityId)));
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
                        .Set(new FlyText(statusEntity, status, isEnfeeblement))
                        .Set(new FlyTextReady(new(status, true, chara->EntityId)))
                        .Add(Ecs.DependsOn, statusEntity);

                    if (statusEntity.TryGet<Condition.StatusIconReplacement>(out var r))
                    {
                        // DefaultTextureScale 1 == low res, 2 == high res
                        var hr = IsUsingHighResTextures() ? "_hr1" : "";
                        var replacementPath = Path.Combine("statuses", $"{r.CustomStatusIconId}{hr}.tex");
                        replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
                        var folder = r.CustomStatusIconId - r.CustomStatusIconId % 1000;
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
