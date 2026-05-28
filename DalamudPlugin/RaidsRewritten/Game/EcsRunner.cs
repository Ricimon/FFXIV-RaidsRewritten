using System;
using Dalamud.Plugin.Services;
using Flecs.NET.Core;
using RaidsRewritten.Log;

namespace RaidsRewritten.Game;

public sealed class EcsRunner : IDalamudHook
{
    private readonly DalamudServices dalamud;
    private readonly ISystem[] systems;
    private readonly ILogger logger;

    private readonly World world;

    public EcsRunner(
        EcsContainer container,
        DalamudServices dalamud,
        CommonQueries commonQueries,
        ISystem[] systems,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.systems = systems;
        this.logger = logger;

        this.world = container.World;

        commonQueries.CreateQueries(world);

//#if DEBUG
//        this.World.Set<flecs.EcsRest>(default);
//#endif

        // Register all systems
        foreach (var system in systems)
        {
            system.Register(world);
        }

        // Create game objects
        Player.Create(world, true);
    }

    public void HookToDalamud()
    {
        dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        dalamud.Framework.Update -= OnFrameworkUpdate;
        foreach(var system in this.systems)
        {
            if (system is IDisposable d)
            {
                d.Dispose();
            }
        }
        world.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // This value isn't always accurate, but is here in case something needs it
        world.Set(new FrameworkDeltaTime(framework.UpdateDelta));
        // Use Flecs self-calculated delta time as this is more accurate than the one reported by IFramework
        world.Progress();
    }
}
