using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ninject.Activation;
using Ninject.Modules;
using RaidsRewritten.Audio;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Attacks.Systems;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Encounters;
using RaidsRewritten.Scripts.Encounters.E1S;
using RaidsRewritten.Scripts.Encounters.UCOB;
using RaidsRewritten.Spawn;
using RaidsRewritten.UI;
using RaidsRewritten.UI.Presenter;
using RaidsRewritten.UI.View;

namespace RaidsRewritten.Ninject;

public class PluginModule : NinjectModule
{
    public override void Load()
    {
        // Dalamud services
        // TODO: Deprecate these
        Bind<IDalamudPluginInterface>().ToConstant(PluginInitializer.PluginInterface).InTransientScope();
        Bind<ICommandManager>().ToConstant(PluginInitializer.CommandManager).InTransientScope();
        Bind<IGameInteropProvider>().ToConstant(PluginInitializer.GameInteropProvider).InTransientScope();
        Bind<IChatGui>().ToConstant(PluginInitializer.ChatGui).InTransientScope();
        Bind<IClientState>().ToConstant(PluginInitializer.ClientState).InTransientScope();
        Bind<ICondition>().ToConstant(PluginInitializer.Condition).InTransientScope();
        Bind<IDutyState>().ToConstant(PluginInitializer.DutyState).InTransientScope();
        Bind<IDataManager>().ToConstant(PluginInitializer.DataManager).InTransientScope();
        Bind<IObjectTable>().ToConstant(PluginInitializer.ObjectTable).InTransientScope();
        Bind<IGameGui>().ToConstant(PluginInitializer.GameGui).InTransientScope();
        Bind<IAddonEventManager>().ToConstant(PluginInitializer.AddonEventManager).InTransientScope();
        Bind<IAddonLifecycle>().ToConstant(PluginInitializer.AddonLifecycle).InTransientScope();
        Bind<IFramework>().ToConstant(PluginInitializer.Framework).InTransientScope();
        Bind<ITextureProvider>().ToConstant(PluginInitializer.TextureProvider).InTransientScope();
        Bind<IKeyState>().ToConstant(PluginInitializer.KeyState).InTransientScope();
        Bind<ISigScanner>().ToConstant(PluginInitializer.SigScanner).InTransientScope();
        Bind<IPluginLog>().ToConstant(PluginInitializer.Log).InTransientScope();

        // External Libraries (and taken code)
        Bind<DalamudServices>().ToSelf();
        Bind<MapEffectProcessor>().ToSelf().InSingletonScope();
        Bind<ObjectEffectProcessor>().ToSelf().InSingletonScope();
        Bind<ActorControlProcessor>().ToSelf().InSingletonScope();
        Bind<ResourceLoader>().ToSelf().InSingletonScope();
        Bind<VfxSpawn>().ToSelf().InSingletonScope();

        // Plugin classes
        Bind<Plugin>().ToSelf().InSingletonScope();
        Bind<IDalamudHook>().To<PluginUIContainer>().InSingletonScope();
        Bind<IDalamudHook>().To<CommandDispatcher>().InSingletonScope();
        Bind<IDalamudHook, EncounterManager>().To<EncounterManager>().InSingletonScope();
        Bind<IDalamudHook>().To<AttackManager>().InSingletonScope();
        Bind<KeyStateWrapper>().ToSelf().InSingletonScope();
        Bind<IAudioDeviceController, AudioDeviceController>().To<AudioDeviceController>().InSingletonScope();
        Bind<ServerConnection>().ToSelf().InSingletonScope();
        Bind<Spatializer>().ToSelf().InSingletonScope();
        Bind<MapManager>().ToSelf().InSingletonScope();
        Bind<Mechanic.Factory>().ToSelf();
        Bind<EcsContainer>().ToSelf().InSingletonScope();
        Bind<CommonQueries>().ToSelf().InSingletonScope();
        // Native control overrides
        Bind<PlayerManager>().ToSelf().InSingletonScope();
        Bind<PlayerMovementOverride>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<PlayerCameraOverride>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<ActionManagerEx>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();

        // Views and Presenters
        Bind<WindowSystem>().ToMethod(_ => new(PluginInitializer.Name)).InSingletonScope();
        Bind<IPluginUIView, EffectsRenderer>().To<EffectsRenderer>().InSingletonScope();
        Bind<IPluginUIPresenter, EffectsRendererPresenter>().To<EffectsRendererPresenter>().InSingletonScope();
        Bind<IPluginUIView, MainWindow>().To<MainWindow>().InSingletonScope();
        Bind<IPluginUIPresenter, MainWindowPresenter>().To<MainWindowPresenter>().InSingletonScope();

        // Data
        Bind<Configuration>().ToMethod(GetConfiguration).InSingletonScope();

        // Encounters
        Bind<IEncounter>().To<UcobRewritten>().InSingletonScope();
        Bind<IEncounter>().To<EdenPrimeTest>().InSingletonScope();

        // Attacks & Systems
        Bind<IAttack>().To<CircleOmen>();
        Bind<IAttack>().To<FanOmen>();
        Bind<IAttack>().To<RectangleOmen>();
        Bind<IAttack>().To<StarOmen>();
        Bind<IAttack>().To<ExaflareOmen>();
        Bind<IAttack, ISystem>().To<Twister>();
        Bind<IAttack, ISystem>().To<RollingBall>();
        Bind<IAttack, ISystem>().To<Fan>();
        Bind<IAttack, ISystem>().To<Circle>();
        Bind<IAttack, ISystem>().To<Scripts.Attacks.LightningCorridor>();
        Bind<IAttack, ISystem>().To<Exaflare>();
        Bind<IAttack, ISystem>().To<ExaflareRow>();
        Bind<IAttack, ISystem>().To<Scripts.Attacks.LiquidHeaven>();
        Bind<IAttack, ISystem>().To<JumpableShockwave>();
        Bind<IAttack, ISystem>().To<Dreadknight>();
        Bind<IAttack, ISystem>().To<ADS>();
        Bind<IAttack, ISystem>().To<DistanceSnapshotTether>();
        Bind<IAttack, ISystem>().To<ExpandingPuddle>();
        Bind<ISystem>().To<Player>();
        Bind<ISystem>().To<DelayedAction>();
        Bind<ISystem>().To<VfxSystem>();
        Bind<ISystem>().To<ModelSystem>();

        // Conditions
        Bind<ISystem>().To<Condition>();
        Bind<IDalamudHook>().To<Knockback>();
        Bind<ISystem>().To<Temperature>();
        Bind<ISystem>().To<Paralysis>();

        Bind<ILogger>().To<DalamudLogger>();
        Bind<DalamudLoggerFactory>().ToSelf();
    }

    private Configuration GetConfiguration(IContext context)
    {
        var configuration = 
            PluginInitializer.PluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();
        configuration.Initialize(PluginInitializer.PluginInterface);
        return configuration;
    }
}
