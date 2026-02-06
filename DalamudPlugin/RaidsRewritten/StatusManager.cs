using RaidsRewritten.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten;

public class StatusManager(
        Configuration configuration,
        StatusCommonProcessor statusCommonProcessor,
        StatusProcessor statusProcessor,
        StatusCustomProcessor statusCustomProcessor,
        StatusPartyListProcessor statusPartyListProcessor,
        StatusTargetInfoBuffDebuffProcessor statusTargetInfoBuffDebuffProcessor,
        StatusFocusTargetProcessor statusFocusTargetProcessor) : IDisposable
{
    private readonly Configuration configuration = configuration;

    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly StatusProcessor statusProcessor = statusProcessor;
    private readonly StatusCustomProcessor statusCustomProcessor = statusCustomProcessor;
    private readonly StatusPartyListProcessor statusPartyListProcessor = statusPartyListProcessor;
    private readonly StatusTargetInfoBuffDebuffProcessor statusTargetInfoBuffDebuffProcessor = statusTargetInfoBuffDebuffProcessor;
    private readonly StatusFocusTargetProcessor statusFocusTargetProcessor = statusFocusTargetProcessor;

    public void Dispose()
    {
        HideAll();
    }

    public void HideAll()
    {
        statusProcessor.HideAll();
        statusCustomProcessor.HideAll();
        statusPartyListProcessor.HideAll();
        statusTargetInfoBuffDebuffProcessor.HideAll();
        statusFocusTargetProcessor.HideAll();
    }
}
