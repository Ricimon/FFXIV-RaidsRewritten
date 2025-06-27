using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using RaidsRewritten.Log;

namespace RaidsRewritten;

public class Plugin(
    IDalamudPluginInterface pluginInterface,
    IEnumerable<IDalamudHook> dalamudHooks,
    ILogger logger)
{
    private IDalamudPluginInterface PluginInterface { get; init; } = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
    private IEnumerable<IDalamudHook> DalamudHooks { get; init; } = dalamudHooks ?? throw new ArgumentNullException(nameof(dalamudHooks));
    private ILogger Logger { get; init; } = logger ?? throw new ArgumentNullException(nameof(logger));

    public void Initialize()
    {
        foreach (var dalamudHook in this.DalamudHooks)
        {
            dalamudHook.HookToDalamud();
        }

        Logger.Info("{0} initialized", PluginInitializer.Name);
    }
}
