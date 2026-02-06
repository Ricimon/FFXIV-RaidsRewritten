using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Conditions;
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
            .With<Player.LocalPlayer>()
            .Up()
            .Event(Ecs.OnSet)
            .Each((Entity e, ref Condition.Status status) =>
            {
                if (!configuration.UseLegacyStatusRendering)
                {
                    //logger.Debug($"ADD: {status.Icon} {status.Title} {status.Description}");
                    var chara = (Character*)StatusCommonProcessor.LocalPlayer();
                    if (chara == null || !chara->IsCharacter()) { return; }
                    statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, true, chara->EntityId));
                }
            });
        world.Observer<Condition.Status>()
            .With<Player.LocalPlayer>()
            .Up()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Condition.Status status) =>
            {
                if (!configuration.UseLegacyStatusRendering)
                {
                    // ensure tooltip doesn't get stuck when debuff expires while showing tooltip
                    statusCommonProcessor.DisableActiveTooltip();
                    //logger.Debug($"REMOVE: {status.Icon} {status.Title} {status.Description}");
                    var chara = (Character*)StatusCommonProcessor.LocalPlayer();
                    if (chara == null || !chara->IsCharacter()) { return; }
                    statusFlyPopupTextProcessor.Value.Enqueue(new(e, status, false, chara->EntityId));

                }
            });
    }
}
