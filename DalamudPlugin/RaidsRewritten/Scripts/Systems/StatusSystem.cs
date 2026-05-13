using FFXIVClientStructs.FFXIV.Client.Game.Character;
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
    Configuration configuration,
    DalamudServices dalamud,
    StatusCommonProcessor statusCommonProcessor,
    Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor,
    ILogger logger) : ISystem
{
    private readonly Configuration configuration = configuration;
    private readonly DalamudServices dalamud = dalamud;
    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor = statusFlyPopupTextProcessor;
    private readonly ILogger logger = logger;

    public void Register(World world)
    {
        world.Observer<Condition.Status>()
            .With<Condition.StatusEnhancement>()
            .With<Player.LocalPlayer>().Up()
            .Event(Ecs.OnSet)
            .Each((e, ref status) => HandleApplyStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusEnfeeblement>()
            .With<Player.LocalPlayer>().Up()
            .Event(Ecs.OnSet)
            .Each((e, ref status) => HandleApplyStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusOther>()
            .With<Player.LocalPlayer>().Up()
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
        //logger.Debug("STATUS APPLY");
        if (!configuration.EverythingDisabled && !configuration.UseLegacyStatusRendering)
        {

            var chara = (Character*)StatusCommonProcessor.LocalPlayer();
            if (chara == null || !chara->IsCharacter()) { return; }

            // handle extended statuses
            statusEntity.Children(Ecs.DependsOn, (Entity e) =>
            {
                e.Destruct();
            });

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
                    var replacementPath = Path.Combine("statuses", $"{r.CustomStatusIconId}_hr1.tex");
                    replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
                    var folder = r.CustomStatusIconId - r.CustomStatusIconId % 1000;
                    var fr = new FileReplacement($"ui/icon/{folder:D6}/{r.IconToReplace}_hr1.tex", replacementPath);
                    flytext.Set(fr);
                    statusEntity.Set(new FileReplacementReference(fr));
                }
            }, 0, true);
        }
    }
}
