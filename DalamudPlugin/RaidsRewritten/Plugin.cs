using System.Collections.Generic;
using Dalamud.Plugin;
using RaidsRewritten.Log;

namespace RaidsRewritten;

public class Plugin(
    IDalamudPluginInterface pluginInterface,
    IEnumerable<IDalamudHook> dalamudHooks,
    ILogger logger)
{
    private IDalamudPluginInterface PluginInterface { get; init; } = pluginInterface;
    private IEnumerable<IDalamudHook> DalamudHooks { get; init; } = dalamudHooks;
    private ILogger Logger { get; init; } = logger;

    public void Initialize()
    {
        foreach (var dalamudHook in this.DalamudHooks)
        {
            dalamudHook.HookToDalamud();
        }

        Logger.Info("{0} initialized", PluginInitializer.Name);
    }
}
