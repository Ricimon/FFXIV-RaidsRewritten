// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/ActorControlProcessor.cs
using ECommons.EzHookManager;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using System;

namespace RaidsRewritten.Memory;

public class ActorControlProcessor : IDisposable
{
    private delegate void ProcessPacketActorControlDelegate(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, ulong targetId, byte replaying);
    private EzHook<ProcessPacketActorControlDelegate> ProcessPacketActorControlHook;

    private Action<uint, uint, uint, uint, uint, uint, uint, uint, ulong, byte>? callback;

    public ActorControlProcessor(ILogger logger)
    {
        try
        {
            ProcessPacketActorControlHook = new EzHook<ProcessPacketActorControlDelegate>("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", ProcessPacketActorControlDetour);
            ProcessPacketActorControlHook?.Enable();
        }
        catch(Exception ex)
        {
            logger.Error($"Could not create ActorControl hook.");
            logger.Error(ex.ToStringFull());
        }
    }

    public void Init(Action<uint, uint, uint, uint, uint, uint, uint, uint, ulong, byte> callback)
    {
        this.callback = callback;
    }

    public void Dispose()
    {
        ProcessPacketActorControlHook?.Disable();
    }

    private void ProcessPacketActorControlDetour(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, ulong targetId, byte replaying)
    {
        ProcessPacketActorControlHook.Original(sourceId, command, p1, p2, p3, p4, p5, p6, targetId, replaying);
        this.callback?.Invoke(sourceId, command, p1, p2, p3, p4, p5, p6, targetId, replaying);
    }
}
