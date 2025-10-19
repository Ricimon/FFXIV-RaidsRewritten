﻿// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/ObjectEffectProcessor.cs
// 0054cc3
using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Memory;

public unsafe class ObjectEffectProcessor(DalamudServices dalamud, ILogger logger) : IDisposable
{
    internal delegate long ProcessObjectEffect(GameObject* a1, ushort a2, ushort a3, long a4);
    [Signature("4C 8B DC 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 6B F0 48 8B D9 49 89 7B E0", DetourName = nameof(ProcessObjectEffectDetour), Fallibility = Fallibility.Fallible)]
    internal Hook<ProcessObjectEffect> ProcessObjectEffectHook = null;
    internal long ProcessObjectEffectDetour(GameObject* a1, ushort a2, ushort a3, long a4)
    {
        try
        {
            //if (P.Config.Logging)
            //{
            //    var text = $"ObjectEffect: on {a1->Name.Read()} 0x{a1->EntityId:X}/0x{a1->BaseId:X} data {a2}, {a3}";
            //    this.logger.Info(text);
            //    if (a1->ObjectKind != FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc) P.LogWindow.Log(text);
            //}
            var ptr = (nint)a1;
            if (!AttachedInfo.ObjectEffectInfos.ContainsKey(ptr))
            {
                AttachedInfo.ObjectEffectInfos[ptr] = [];
            }
            AttachedInfo.ObjectEffectInfos[ptr].Add(new()
            {
                StartTime = Environment.TickCount64,
                data1 = a2,
                data2 = a3
            });
            this.callback?.Invoke(a1->EntityId, a2, a3);
        }
        catch (Exception e)
        {
            logger.Info(e.ToStringFull());
        }
        return ProcessObjectEffectHook.Original(a1, a2, a3, a4);
    }

    private Action<uint, ushort, ushort>? callback;

    public void Init(Action<uint, ushort, ushort> callback)
    {
        this.callback = callback;
        try
        {
            dalamud.GameInteropProvider.InitializeFromAttributes(this);
        }
        catch (Exception e)
        {
            logger.Warn(e.ToStringFull());
        }
        Enable();
    }

    public void Dispose()
    {
        try
        {
            Disable();
            ProcessObjectEffectHook.Dispose();
        }
        catch (Exception e)
        {
            logger.Warn(e.ToStringFull());
        }
    }

    internal void Enable()
    {
        try
        {
            if (!ProcessObjectEffectHook.IsEnabled) ProcessObjectEffectHook.Enable();
        }
        catch (Exception e)
        {
            logger.Warn(e.ToStringFull());
        }
    }

    internal void Disable()
    {
        try
        {
            if (ProcessObjectEffectHook.IsEnabled) ProcessObjectEffectHook.Disable();
        }
        catch (Exception e)
        {
            logger.Warn(e.ToStringFull());
        }
    }
}
