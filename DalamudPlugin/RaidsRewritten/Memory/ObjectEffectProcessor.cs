// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/ObjectEffectProcessor.cs
// 6173f05
using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Memory;

public unsafe sealed class ObjectEffectProcessor(DalamudServices dalamud, ILogger logger) : IDisposable
{
    internal Hook<EventObject.Delegates.PlayAnimation> ProcessObjectEffectHook = null;
    internal void ProcessObjectEffectDetour(EventObject* thisPtr, uint entityId, uint actionId, ulong a4)
    {
        try
        {
            //if(P.Config.Logging)
            //{
            //    var text = $"ObjectEffect: on {thisPtr->Name.Read()} {thisPtr->EntityId.Format()}/{thisPtr->BaseId.Format()} data {entityId}, {actionId}";
            //    Logger.Log(text);
            //    if(thisPtr->ObjectKind != ObjectKind.Pc) P.LogWindow.Log(text);
            //}
            var ptr = (nint)thisPtr;
            if (!AttachedInfo.ObjectEffectInfos.ContainsKey(ptr))
            {
                AttachedInfo.ObjectEffectInfos[ptr] = [];
            }
            AttachedInfo.ObjectEffectInfos[ptr].Add(new()
            {
                StartTime = Environment.TickCount64,
                data1 = entityId,
                data2 = actionId
            });
            this.callback?.Invoke(thisPtr->EntityId, entityId, actionId);
        }
        catch (Exception e)
        {
            logger.Info(e.ToStringFull());
        }
        ProcessObjectEffectHook.Original(thisPtr, entityId, actionId, a4);
    }

    private Action<uint, uint, uint>? callback;

    public void Init(Action<uint, uint, uint> callback)
    {
        this.callback = callback;
        try
        {
            ProcessObjectEffectHook = dalamud.GameInteropProvider.HookFromAddress<EventObject.Delegates.PlayAnimation>(EventObject.Addresses.PlayAnimation.Value, this.ProcessObjectEffectDetour);
            ProcessObjectEffectHook.Enable();
        }
        catch (Exception e)
        {
            logger.Warn(e.ToStringFull());
        }
    }

    public void Dispose()
    {
        ProcessObjectEffectHook?.Disable();
        ProcessObjectEffectHook?.Dispose();
    }
}
