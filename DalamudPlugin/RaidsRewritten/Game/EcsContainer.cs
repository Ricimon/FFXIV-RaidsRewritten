using System;
using Dalamud.Plugin.Services;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class EcsContainer : IDisposable
{
    public World World { get; init; }

    private readonly DalamudServices dalamud;
    private readonly CommonQueries commonQueries;
    private readonly ISystem[] systems;
    private readonly ILogger logger;

    public EcsContainer(DalamudServices dalamud, CommonQueries commonQueries, ISystem[] systems, ILogger logger)
    {
        this.dalamud = dalamud;
        this.commonQueries = commonQueries;
        this.systems = systems;
        this.logger = logger;

        this.World = World.Create();
#if DEBUG
        this.World.Set<flecs.EcsRest>(default);
#endif

        this.commonQueries.CreateQueries(this.World);

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
        this.commonQueries.Dispose();
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
