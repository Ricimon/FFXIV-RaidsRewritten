// Adapted from https://github.com/NightmareXIV/ECommons/blob/master/ECommons/Hooks/MapEffect.cs
// 0054cc3
using System;
using ECommons.Hooks;
using RaidsRewritten.Log;

namespace RaidsRewritten.Memory;

public sealed class MapEffectProcessor(
    DalamudServices dalamud,
    ILogger logger) : IDisposable
{
    public void Init(Action<uint, ushort, ushort> callback)
    {
        MapEffect.Init(dalamud.SigScanner, dalamud.GameInteropProvider, logger, (a1, a2, a3, a4) =>
        {
            var text = $"MapEffect: {a2}, {a3}, {a4}";
            callback(a2, a3, a4);
        });
    }

    public void Dispose()
    {
        MapEffect.Dispose();
    }
}
