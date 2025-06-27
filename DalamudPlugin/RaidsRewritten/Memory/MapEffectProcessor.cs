// Adapted from https://github.com/NightmareXIV/ECommons/blob/master/ECommons/Hooks/MapEffect.cs
using System;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using RaidsRewritten.Log;

namespace RaidsRewritten.Memory;

public class MapEffectProcessor : IDisposable
{
    private readonly ISigScanner sigScanner;
    private readonly IGameInteropProvider gameInteropProvider;
    private readonly ILogger logger;

    public MapEffectProcessor(
        ISigScanner sigScanner,
        IGameInteropProvider gameInteropProvider,
        ILogger logger)
    {
        this.sigScanner = sigScanner;
        this.gameInteropProvider = gameInteropProvider;
        this.logger = logger;
    }

    public void Init(Action<uint, ushort, ushort> callback)
    {
        MapEffect.Init(sigScanner, gameInteropProvider, logger, (a1, a2, a3, a4) =>
        {
            var text = $"MapEffect: {a2}, {a3}, {a4}";
            callback(a2, a3, a4);
        });
    }

    public void Dispose()
    {
        MapEffect.Dispose();
        GC.SuppressFinalize(this);
    }
}
