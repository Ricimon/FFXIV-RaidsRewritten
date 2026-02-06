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
        StatusFlyPopupTextProcessor statusFlyPopupTextProcessor,
        StatusFocusTargetProcessor statusFocusTargetProcessor,
        StatusPartyListProcessor statusPartyListProcessor,
        StatusTargetInfoProcessor statusTargetInfoProcessor,
        StatusTargetInfoBuffDebuffProcessor statusTargetInfoBuffDebuffProcessor) : IDisposable
{
    private readonly Configuration configuration = configuration;

    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly StatusProcessor statusProcessor = statusProcessor;
    private readonly StatusCustomProcessor statusCustomProcessor = statusCustomProcessor;
    private readonly StatusFlyPopupTextProcessor statusFlyPopupTextProcessor = statusFlyPopupTextProcessor;
    private readonly StatusFocusTargetProcessor statusFocusTargetProcessor = statusFocusTargetProcessor;
    private readonly StatusPartyListProcessor statusPartyListProcessor = statusPartyListProcessor;
    private readonly StatusTargetInfoProcessor statusTargetInfoProcessor = statusTargetInfoProcessor;
    private readonly StatusTargetInfoBuffDebuffProcessor statusTargetInfoBuffDebuffProcessor = statusTargetInfoBuffDebuffProcessor;

    public void Dispose()
    {
        HideAll();
    }

    public void HideAll()
    {
        statusProcessor.HideAll();
        statusCustomProcessor.HideAll();
        statusPartyListProcessor.HideAll();
        statusTargetInfoProcessor.HideAll();
        statusTargetInfoBuffDebuffProcessor.HideAll();
        statusFocusTargetProcessor.HideAll();
    }
}
