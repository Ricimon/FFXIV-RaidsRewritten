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
    private readonly ISystem[] systems;
    private readonly ILogger logger;

    public EcsContainer(DalamudServices dalamud, ISystem[] systems, ILogger logger)
    {
        this.dalamud = dalamud;
        this.systems = systems;
        this.logger = logger;

        this.World = World.Create();

        // Register all systems
        foreach(var system in systems)
        {
            system.Register(this.World);
        }

        // Create game objects
        Player.Create(this.World, true);

        this.dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.dalamud.Framework.Update -= OnFrameworkUpdate;
        foreach(var system in this.systems)
        {
            if (system is IDisposable d)
            {
                d.Dispose();
            }
        }
        this.World.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            // This value isn't always accurate, but is here in case something needs it
            this.World.Set(new FrameworkDeltaTime(framework.UpdateDelta));
            // Use Flecs self-calculated delta time as this is more accurate than the one reported by IFramework
            this.World.Progress();
        }
        catch(Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }
    }
}
