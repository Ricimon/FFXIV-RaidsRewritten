// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Utils.cs
// fbbfedb
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using System;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    public delegate void BattleLog_AddToScreenLogWithScreenLogKindDelegate(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType);
    public BattleLog_AddToScreenLogWithScreenLogKindDelegate BattleLog_AddToScreenLogWithScreenLogKind;

    // this is for esuna logic so not needed for now?
    //public unsafe void BattleLog_AddToScreenLogWithScreenLogKindDetour(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType)
    //{
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
        //} catch (Exception e)
        //{
        //    logger.Error(e.ToString());
        //}
    //    BattleLog_AddToScreenLogWithScreenLogKind.Original(target, source, kind, a4, a5, actionID, statusID, stackCount, damageType);
    //}

    //private static unsafe bool IsValidDispeller(MyStatus status, Character* chara)
    //    => status.Dispeller.Length is 0 || status.Dispeller == chara->GetNameWithWorld();

    private static bool ProcessPenumbraPath(string path, out string outPath)
    {
        outPath = path;
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.StartsWith('|')) return false;

        var split = path.Split("|");
        if (split.Length != 3) return false;

        outPath = split[2];
        return true;
    }
}
