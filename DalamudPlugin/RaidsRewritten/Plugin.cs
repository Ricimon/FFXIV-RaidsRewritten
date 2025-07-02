using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using RaidsRewritten.Extensions;
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
        try
        {
            foreach (var dalamudHook in this.DalamudHooks)
            {
                dalamudHook.HookToDalamud();
            }

            Logger.Info("{0} initialized", PluginInitializer.Name);
        }
        catch(Exception e)
        {
            Logger.Error(e.ToStringFull());
        }
    }
}
