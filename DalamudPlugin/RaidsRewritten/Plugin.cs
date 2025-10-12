using System.Collections.Generic;
using RaidsRewritten.Log;

namespace RaidsRewritten;

public class Plugin(
    IEnumerable<IDalamudHook> dalamudHooks,
    ILogger logger)
{
    private IEnumerable<IDalamudHook> DalamudHooks { get; init; } = dalamudHooks;
    private ILogger Logger { get; init; } = logger;

    public void Initialize()
    {
        foreach (var dalamudHook in this.DalamudHooks)
        {
            dalamudHook.HookToDalamud();
        }

        Logger.Info($"{PluginInitializer.Name} initialized");
    }
}
