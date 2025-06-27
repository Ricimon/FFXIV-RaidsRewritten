using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ninject;
using Ninject.Extensions.Factory;
using Pictomancy;
using RaidsRewritten.Log;
using RaidsRewritten.Ninject;

namespace RaidsRewritten;

public sealed class PluginInitializer : IDalamudPlugin
{
    public static string Name => "RaidsRewritten";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get ; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonEventManager AddonEventManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly StandardKernel kernel;

    public PluginInitializer()
    {
        //Services
        PictoService.Initialize(PluginInterface);

        this.kernel = new StandardKernel(new PluginModule(), new FuncModule());

        // Logging
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Entrypoint
        this.kernel.Get<Plugin>().Initialize();
    }

    public void Dispose()
    {
        PictoService.Dispose();
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        this.kernel.Dispose();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
#if DEBUG
        // There's a number of unobserved Task exceptions that can be thrown by 3rd party libraries
        // in this project, some of which seem to be unavoidable but harmless.
        this.kernel.Get<ILogger>().Error(e.Exception.ToString());
#else
        // So, lower the severity of the log in release builds.
        this.kernel.Get<ILogger>().Trace(e.Exception.ToString());
#endif
        e.SetObserved();
    }
}
