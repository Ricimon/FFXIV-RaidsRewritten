// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/ActorControlProcessor.cs
// d0d8416
using System;
using ECommons;
using ECommons.EzHookManager;
using ECommons.Logging;

namespace RaidsRewritten.Memory;

public class ActorControlProcessor : IDisposable
{
    private delegate void ProcessPacketActorControlDelegate(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying);
    private EzHook<ProcessPacketActorControlDelegate> ProcessPacketActorControlHook;

    private Action<uint, uint, uint, uint, uint, uint, uint, uint, uint, uint, ulong, byte>? callback;

    public ActorControlProcessor()
    {
        try
        {
            ProcessPacketActorControlHook = new EzHook<ProcessPacketActorControlDelegate>("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", ProcessPacketActorControlDetour);
            ProcessPacketActorControlHook?.Enable();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Could not create ActorControl hook.");
            ex.Log();
        }
    }

    public void Init(Action<uint, uint, uint, uint, uint, uint, uint, uint, uint, uint, ulong, byte> callback)
    {
        this.callback = callback;
    }

    public void Dispose()
    {
        ProcessPacketActorControlHook?.Disable();
    }

    private void ProcessPacketActorControlDetour(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        ProcessPacketActorControlHook.Original(sourceId, command, p1, p2, p3, p4, p5, p6, p7, p8, targetId, replaying);
        this.callback?.Invoke(sourceId, command, p1, p2, p3, p4, p5, p6, p7, p8, targetId, replaying);
    }
}