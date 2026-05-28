using RaidsRewritten.Log;

namespace RaidsRewritten;

public class Plugin(
    IDalamudHook[] dalamudHooks,
    ILogger logger)
{
    public void Initialize()
    {
        foreach (var dalamudHook in dalamudHooks)
        {
            dalamudHook.HookToDalamud();
        }

        logger.Info($"{PluginInitializer.Name} initialized");
    }
}
