using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Ninject;
using Ninject.Extensions.Factory;
using Ninject.Planning.Bindings.Resolvers;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Ninject;
using RaidsRewritten.Utility;

namespace RaidsRewritten;

public sealed class PluginInitializer : IDalamudPlugin
{
    public static string Name => "RaidsRewritten";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IAddonEventManager AddonEventManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly StandardKernel kernel;

    public PluginInitializer()
    {
        this.kernel = new StandardKernel(new PluginModule(), new FuncModule());
        // Remove implicit bindings - all bindings must be explicitly declared
        this.kernel.Components.Remove<IMissingBindingResolver, SelfBindingResolver>();

        //Services
        Svc.Init(PluginInterface, this.kernel.Get<ILogger>());

        // Logging
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling(e => this.kernel.Get<ILogger>().Error(e.ToStringFull()));

        // Entrypoint
        this.kernel.Get<Plugin>().Initialize();
    }

    public void Dispose()
    {
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        var debug = false;
#if DEBUG
        debug = true;
#endif
        var logger = this.kernel.Get<ILogger>();
        // Because of unordered disposal in the kernel, that any Flecs operations can crash after World disposal,
        // and there not being a reliable way to check if the World has been disposed, the World disposal operation
        // is moved outside of the kernel, and World.Quit() is used inside. See EcsRunner.
        var ecsContainer = this.kernel.Get<EcsContainer>();

        if (debug)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            this.kernel.Dispose();
            logger.Debug("Kernel disposal took {0}ms", stopwatch.ElapsedMilliseconds);

            stopwatch.Restart();
            ecsContainer.World.Dispose();
            logger.Debug("Flecs World disposal took {0}ms", stopwatch.ElapsedMilliseconds);
        }
        else
        {
            this.kernel.Dispose();
            ecsContainer.World.Dispose();
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var exceptionMessage = e.Exception.ToStringFull();
            this.kernel.Get<ILogger>().Error(exceptionMessage);
        }
        catch (FormatException) { return; }
        e.SetObserved();
    }
}
