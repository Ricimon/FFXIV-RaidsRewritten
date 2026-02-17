// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/Memory/Memory.cs
// 0ed07e8
using Dalamud.Hooking;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    public delegate char LoadIconByIDDelegate(void* iconText, int iconId);
    public LoadIconByIDDelegate LoadIconByID;

    public delegate void AtkComponentIconText_ReceiveEvent(nint a1, short a2, nint a3, nint a4, nint a5);
    public Hook<AtkComponentIconText_ReceiveEvent> AtkComponentIconTextReceiveEventHook { get; private set; }

    private void AtkComponentIconText_ReceiveEventDetour(nint a1, short a2, nint a3, nint a4, nint a5)
    {
        try
        {
            //PluginLog.Debug($"{a1:X16}, {a2}, {a3:X16}, {a4:X16}, {a5:X16}");
            if (a2 == 6)
            {
                statusCommonProcessor.Value.HoveringOver = a1;
            }
            if (a2 == 7)
            {
                statusCommonProcessor.Value.HoveringOver = 0;
            }
            //// Handle Cancellation Request on Right Click
            //if (a2 == 9 && P.CommonProcessor.WasRightMousePressed)
            //{
            //    // We dunno what status this is yet, so mark the address for next check.
            //    P.CommonProcessor.CancelRequests.Add(a1);
            //    P.CommonProcessor.HoveringOver = 0;
            //}
        } catch (Exception e)
        {
            logger.Error(e.ToStringFull());
        }
        AtkComponentIconTextReceiveEventHook.Original(a1, a2, a3, a4, a5);
    }
}
