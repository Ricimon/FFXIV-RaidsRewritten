using System;
using Dalamud.Plugin.Services;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;

namespace RaidsRewritten.Game;

public sealed class EcsContainer : IDisposable
{
    public World World { get; init; }

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    public EcsContainer(DalamudServices dalamud, ISystem[] systems, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        this.World = World.Create();

        // Register all systems
        foreach(var system in systems)
        {
            system.Register(this.World);
        }

        // Create game objects
        Player.Create(this.World);

        this.dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.dalamud.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            this.World.Progress((float)framework.UpdateDelta.TotalSeconds);
        }
        catch(Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }
    }
}
