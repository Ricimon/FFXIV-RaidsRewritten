using Dalamud.Interface.Windowing;
using Ninject.Activation;
using Ninject.Modules;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Encounters;
using RaidsRewritten.Scripts.Encounters.E1S;
using RaidsRewritten.Scripts.Encounters.UCOB;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Scripts.Systems;
using RaidsRewritten.Spawn;
using RaidsRewritten.UI;
using RaidsRewritten.UI.View;

namespace RaidsRewritten.Ninject;

public class PluginModule : NinjectModule
{
    public override void Load()
    {
        // External Libraries (and taken code)
        Bind<DalamudServices>().ToSelf();
        Bind<MapEffectProcessor>().ToSelf().InSingletonScope();
        Bind<ObjectEffectProcessor>().ToSelf().InSingletonScope();
        Bind<ActorControlProcessor>().ToSelf().InSingletonScope();
        Bind<ResourceLoader>().ToSelf().InSingletonScope();
        Bind<VfxSpawn>().ToSelf().InSingletonScope();
        Bind<StatusCommonProcessor>().ToSelf().InSingletonScope();
        Bind<StatusCustomProcessor>().ToSelf().InSingletonScope();
        Bind<StatusPartyListProcessor>().ToSelf().InSingletonScope();

        // Plugin classes
        Bind<Plugin>().ToSelf().InSingletonScope();
        Bind<IDalamudHook>().To<PluginUIContainer>().InSingletonScope();
        Bind<IDalamudHook>().To<CommandDispatcher>().InSingletonScope();
        Bind<IDalamudHook, EncounterManager>().To<EncounterManager>().InSingletonScope();
        Bind<IDalamudHook>().To<EntityManager>().InSingletonScope();
        Bind<Mechanic.Factory>().ToSelf();
        Bind<InputEventSource>().ToSelf().InSingletonScope();
        Bind<EcsContainer>().ToSelf().InSingletonScope();
        Bind<CommonQueries>().ToSelf().InSingletonScope();
        Bind<NetworkClient>().ToSelf().InSingletonScope();
        Bind<NetworkClientMessageHandler>().ToSelf().WhenInjectedInto<NetworkClient>().InSingletonScope();
        Bind<NetworkClientUi>().ToSelf();
        // Native control overrides
        Bind<PlayerManager>().ToSelf().InSingletonScope();
        Bind<PlayerMovementOverride>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<PlayerCameraOverride>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<ActionManagerEx>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<HotbarManager>().ToSelf().WhenInjectedInto<PlayerManager>().InSingletonScope();
        Bind<StatusManager>().ToSelf().InSingletonScope();

        // Views and Presenters
        Bind<WindowSystem>().ToMethod(_ => new(PluginInitializer.Name)).InSingletonScope();
        Bind<IPluginUIView, EffectsRenderer>().To<EffectsRenderer>().InSingletonScope();
        Bind<IPluginUIView, MainWindow>().To<MainWindow>().InSingletonScope();
        Bind<IPluginUIView, HelpWindow>().To<HelpWindow>().InSingletonScope();
        Bind<IPluginUIView, ChangelogWindow>().To<ChangelogWindow>().InSingletonScope();

        // Data
        Bind<Configuration>().ToMethod(GetConfiguration).InSingletonScope();

        // Encounters
        Bind<IEncounter>().To<UcobRewritten>().InSingletonScope();
        Bind<IEncounter>().To<EdenPrimeTest>().InSingletonScope();

        // Entities
        // Models
        Bind<IEntity>().To<Chefbingus>();
        // Omens
        Bind<IEntity>().To<CircleOmen>();
        Bind<IEntity>().To<Fan90Omen>();
        Bind<IEntity>().To<Fan120Omen>();
        Bind<IEntity>().To<RectangleOmen>();
        Bind<IEntity>().To<ShortStarOmen>();
        Bind<IEntity>().To<LongStarOmen>();
        Bind<IEntity>().To<ExaflareOmen>();
        Bind<IEntity>().To<OneThirdDonutOmen>();
        // Attacks
        Bind<IEntity, ISystem>().To<TwisterObstacleCourse>();
        Bind<IEntity, ISystem>().To<Twister>();
        Bind<IEntity, ISystem>().To<RollingBall>();
        Bind<IEntity, ISystem>().To<Fan>();
        Bind<IEntity, ISystem>().To<Circle>();
        Bind<IEntity, ISystem>().To<Scripts.Attacks.LightningCorridor>();
        Bind<IEntity, ISystem>().To<Exaflare>();
        Bind<IEntity, ISystem>().To<ExaflareRow>();
        Bind<IEntity, ISystem>().To<Scripts.Attacks.LiquidHeaven>();
        Bind<IEntity, ISystem>().To<JumpableShockwave>();
        Bind<IEntity, ISystem>().To<Dreadknight>();
        Bind<IEntity, ISystem>().To<ADS>();
        Bind<IEntity, ISystem>().To<DistanceSnapshotTether>();
        Bind<IEntity, ISystem>().To<ExpandingPuddle>();
        Bind<IEntity, ISystem>().To<Star>();
        Bind<IEntity, ISystem>().To<Tornado>();
        Bind<IEntity, ISystem>().To<OctetDonut>();
        Bind<IEntity, ISystem>().To<RepellingCannonADS>();
        Bind<IEntity, ISystem>().To<CircleBladeMelusine>();
        Bind<IEntity, ISystem>().To<NerveGasKaliya>();
        Bind<IEntity, ISystem>().To<VoidGate>();
        
        // Systems
        Bind<ISystem>().To<Player>();
        Bind<ISystem>().To<DelayedAction>();
        Bind<ISystem>().To<VfxSystem>();
        Bind<ISystem>().To<OmenSystem>();
        Bind<ISystem>().To<ModelSystem>();
        Bind<ISystem>().To<FileReplacementSystem>();
        Bind<ISystem>().To<InputSystem>();
        Bind<ISystem>().To<NetworkClientPositionSystem>();

        // Conditions
        Bind<ISystem>().To<Condition>();
        Bind<IDalamudHook>().To<Knockback>();
        Bind<ISystem>().To<Temperature>();
        Bind<ISystem>().To<Paralysis>();
        Bind<ISystem>().To<Hysteria>();

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
