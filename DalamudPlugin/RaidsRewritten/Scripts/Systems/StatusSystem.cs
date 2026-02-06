using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Scripts.Systems;

public unsafe class StatusSystem(Configuration configuration, StatusCommonProcessor statusCommonProcessor) : ISystem
{
    private readonly Configuration configuration = configuration;
    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;

    public void Register(World world)
    {
        world.Observer<Condition.Status>()
            .Event(Ecs.OnRemove)
            .Each((ref status) =>
            {
                // ensure tooltip doesn't get stuck when debuff expires while showing tooltip
                if (!configuration.DisableCustomStatuses)
                {
                    statusCommonProcessor.DisableActiveTooltip();
                }
            });
    }
}
