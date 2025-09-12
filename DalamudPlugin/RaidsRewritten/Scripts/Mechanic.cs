using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts;

public abstract class Mechanic()
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected DalamudServices Dalamud { get; private set; }
    protected Flecs.NET.Core.World World { get; private set; }
    protected CommonQueries CommonQueries { get; private set; }
    protected AttackManager AttackManager { get; private set; }
    protected VfxSpawn VfxSpawn { get; private set; }
    protected ILogger Logger { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private void Init(
        DalamudServices dalamud,
        EcsContainer ecsContainer,
        CommonQueries commonQueries,
        AttackManager attackManager,
        VfxSpawn vfxSpawn,
        ILogger logger)
    {
        this.Dalamud = dalamud;
        this.World = ecsContainer.World;
        this.CommonQueries = commonQueries;
        this.AttackManager = attackManager;
        this.VfxSpawn = vfxSpawn;
        this.Logger = logger;
    }

    public virtual void Reset() { }

    public virtual void OnFrameworkUpdate(IFramework framework) { }

    public virtual void OnDirectorUpdate(DirectorUpdateCategory a3) { }

    public virtual void OnObjectCreation(nint newObjectPointer, IGameObject? newObject) { }

    public virtual void OnActionEffectEvent(ActionEffectSet set) { }

    public virtual void OnVFXSpawn(IGameObject? target, string vfxPath) { }

    public virtual void OnStartingCast(Action action, IBattleChara source) { }

    public virtual void OnCombatStart() { }

    public virtual void OnCombatEnd() { }

    public virtual void OnWeatherChange(byte weather) { }

    public class Factory(
        DalamudServices dalamud,
        EcsContainer ecsContainer,
        CommonQueries commonQueries,
        AttackManager attackManager,
        VfxSpawn vfxSpawn,
        ILogger logger)
    {
        public T Create<T>() where T : Mechanic, new()
        {
            var mechanic = new T();
            mechanic.Init(dalamud, ecsContainer, commonQueries, attackManager, vfxSpawn, logger);
            return mechanic;
        }
    }
}
