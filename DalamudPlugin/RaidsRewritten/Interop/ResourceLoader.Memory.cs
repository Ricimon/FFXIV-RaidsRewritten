// Adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/Memory/Memory.cs
// 3a67416
// and https://github.com/kawaii/Moodles/blob/main/Moodles/Memory/FlyText.cs
// 2fb7c30
using Dalamud.Game.Gui.FlyText;
using ECommons.EzHookManager;
using RaidsRewritten.Utility;
using System;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    // Memory.cs
    public delegate nint AtkComponentIconText_LoadIconByIDDelegate(void* iconText, int iconId);
    public AtkComponentIconText_LoadIconByIDDelegate AtkComponentIconText_LoadIconByID = EzDelegate.Get<AtkComponentIconText_LoadIconByIDDelegate>("E8 ?? ?? ?? ?? 41 8D 45 3E");

    private delegate void AtkComponentIconText_ReceiveEvent(nint a1, short a2, nint a3, nint a4, nint a5);
    [EzHook("44 0F B7 C2 4D 8B D1")]
    private EzHook<AtkComponentIconText_ReceiveEvent> AtkComponentIconText_ReceiveEventHook;
    public event Action<nint>? OnAtkComponentIconText_ReceiveHoverEvent;

    private void AtkComponentIconText_ReceiveEventDetour(nint a1, short a2, nint a3, nint a4, nint a5)
    {
        try
        {
            if (a2 == 6)
            {
                OnAtkComponentIconText_ReceiveHoverEvent?.Invoke(a1);
            }
            if (a2 == 7)
            {
                OnAtkComponentIconText_ReceiveHoverEvent?.Invoke(0);
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
        AtkComponentIconText_ReceiveEventHook.Original(a1, a2, a3, a4, a5);
    }

    // FlyText.cs
    public delegate void BattleLog_AddToScreenLogWithScreenLogKind(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType);
    [EzHook("48 85 C9 0F 84 ?? ?? ?? ?? 56 41 56", nameof(BattleLog_AddToScreenLogWithScreenLogKindDetour))]
    public EzHook<BattleLog_AddToScreenLogWithScreenLogKind> BattleLog_AddToScreenLogWithScreenLogKindHook;

    public unsafe void BattleLog_AddToScreenLogWithScreenLogKindDetour(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType)
    {
        // this is for esuna logic so not needed for now?
        //try
        //{
        //    if (C.Debug)
        //    {
        //        PluginLog.Verbose($"BattleLog_AddActionLogMessageDetour: {target:X16}, {source:X16}, {kind}, {a4}, {a5}, {actionID}, {statusID}, {stackCount}, {damageType}");
        //    }
        //    // If Moodles can be Esunad
        //    if (C.MoodlesCanBeEsunad)
        //    {
        //        // If action is Esuna
        //        if (actionID == 7568 && kind == FlyTextKind.HasNoEffect)
        //        {
        //            // Only check logic if the source and target are valid actors.
        //            if (CharaWatcher.TryGetValue(source, out Character* chara) && CharaWatcher.TryGetValue(target, out Character* targetChara))
        //            {
        //                // Check permission (Must be allowing from others, or must be from self)
        //                if (C.OthersCanEsunaMoodles || chara->ObjectIndex == 0)
        //                {
        //                    // Grab the status manager. (Do not trigger on Ephemeral, wait for them to update via IPC)
        //                    if (targetChara->MyStatusManager() is { } manager && !manager.Ephemeral)
        //                    {
        //                        bool fromClient = chara->ObjectIndex == 0;

        //                        foreach (MyStatus status in manager.Statuses)
        //                        {
        //                            // Ensure only negative statuses are dispelled.
        //                            if (status.Type != StatusType.Negative) continue;
        //                            // If it cannot be dispelled, skip it.
        //                            else if (!status.Modifiers.Has(Modifiers.CanDispel)) continue;
        //                            // Client cannot dispel locked statuses.
        //                            else if (fromClient && manager.LockedIds.Contains(status.GUID)) continue;
        //                            // Prevent dispelling if not from client and others are not allowed.
        //                            else if (!fromClient && !C.OthersCanEsunaMoodles) continue;
        //                            // Others cannot dispel if they are not whitelisted.
        //                            else if (!IsValidDispeller(status, chara)) continue;

        //                            // Perform the dispel, expiring the timer. Also apply the chain if desired.
        //                            status.ExpiresAt = 0;
        //                            if (status.ChainedStatus != Guid.Empty && status.ChainTrigger is ChainTrigger.Dispel)
        //                            {
        //                                status.ApplyChain = true;
        //                            }
        //                            // This return is to not show the failed message
        //                            return;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    if (UI.Suppress) return;
        //}
        //catch (Exception e)
        //{
        //    e.Log();
        //}
        BattleLog_AddToScreenLogWithScreenLogKindHook.Original(target, source, kind, a4, a5, actionID, statusID, stackCount, damageType);
    }

    //private static unsafe bool IsValidDispeller(MyStatus status, Character* chara)
    //    => status.Dispeller.Length is 0 || status.Dispeller == chara->GetNameWithWorld();
}
