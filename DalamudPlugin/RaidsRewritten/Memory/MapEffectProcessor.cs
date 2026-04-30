// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/MapEffectProcessor.cs
// 3d62c12
using System;
using System.Collections.Generic;
using ECommons.Hooks;
using RaidsRewritten.Log;

namespace RaidsRewritten.Memory;

public sealed class MapEffectProcessor(
    DalamudServices dalamud,
    ILogger logger) : IDisposable
{
    //public static Dictionary<uint, (ushort Param1, ushort Param2)> History = [];
    public void Init(Action<uint, ushort, ushort> callback)
    {
        MapEffect.Init(dalamud.SigScanner, dalamud.GameInteropProvider, logger, (a1, a2, a3, a4) =>
        {
            var text = $"MapEffect: {a2}, {a3}, {a4}";
            //History[a2] = (a3, a4);
            callback(a2, a3, a4);
        });
    }

    public void Dispose()
    {
        MapEffect.Dispose();
    }
}
