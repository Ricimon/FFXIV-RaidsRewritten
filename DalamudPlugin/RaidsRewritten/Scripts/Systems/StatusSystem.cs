using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Scripts.Systems;

public unsafe class StatusSystem(
    Configuration configuration,
    StatusCommonProcessor statusCommonProcessor,
    Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor,
    ILogger logger) : ISystem
{
    private readonly Configuration configuration = configuration;
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

        world.Observer<Condition.Status>()
            .With<Condition.StatusEnhancement>()
            .With<Player.LocalPlayer>().Up()
            .Event(Ecs.OnRemove)
            .Each((e, ref status) => HandleRemoveStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusEnfeeblement>()
            .With<Player.LocalPlayer>().Up()
            .Event(Ecs.OnRemove)
            .Each((e, ref status) => HandleRemoveStatus(e, status));
        world.Observer<Condition.Status>()
            .With<Condition.StatusOther>()
            .With<Player.LocalPlayer>().Up()
            .Event(Ecs.OnRemove)
            .Each((e, ref status) => HandleRemoveStatus(e, status));
    }

    private void HandleApplyStatus(Entity e, Condition.Status status)
    {
        if (!configuration.EverythingDisabled && !configuration.UseLegacyStatusRendering)
        {
            var chara = (Character*)StatusCommonProcessor.LocalPlayer();
            if (chara == null || !chara->IsCharacter()) { return; }

            if (e.TryGet<FileReplacement>(out var replacement))
            {
                statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, true, chara->EntityId, replacement));
            } else
            {
                statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, true, chara->EntityId));
            }
        }
    }

    private void HandleRemoveStatus(Entity e, Condition.Status status)
    {
        if (!configuration.EverythingDisabled && !configuration.UseLegacyStatusRendering)
        {
            // ensure tooltip doesn't get stuck when debuff expires while showing tooltip
            statusCommonProcessor.DisableActiveTooltip();
            //logger.Debug($"REMOVE: {status.Icon} {status.Title} {status.Description}");
            var chara = (Character*)StatusCommonProcessor.LocalPlayer();
            if (chara == null || !chara->IsCharacter()) { return; }
            if (e.TryGet<FileReplacement>(out var replacement))
            {
                statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, false, chara->EntityId, replacement));
            } else
            {
                statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, false, chara->EntityId));
            }

        }
    }
}
