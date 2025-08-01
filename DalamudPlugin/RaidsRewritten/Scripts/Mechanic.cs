using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using ECommons.Hooks.ActionEffectTypes;
using RaidsRewritten.Log;
using Lumina.Excel.Sheets;

namespace RaidsRewritten.Scripts;

public abstract class Mechanic()
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected DalamudServices Dalamud { get; private set; }
    protected Flecs.NET.Core.World World { get; private set; }
    protected AttackManager AttackManager { get; private set; }
    protected ILogger Logger { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private void Init(
        DalamudServices dalamud,
        EcsContainer ecsContainer,
        AttackManager attackManager,
        ILogger logger)
    {
        this.Dalamud = dalamud;
        this.World = ecsContainer.World;
        this.AttackManager = attackManager;
        this.Logger = logger;
    }

    public virtual void Reset() { }

    public virtual void OnDirectorUpdate(DirectorUpdateCategory a3) { }

    public virtual void OnObjectCreation(nint newObjectPointer, IGameObject? newObject) { }

    public virtual void OnActionEffectEvent(ActionEffectSet set) { }

    public virtual void OnVFXSpawn(IGameObject? target, string vfxPath) { }

    public virtual void OnStartingCast(Action action, IBattleChara source) { }

    public virtual void OnCombatStart() { }

    public virtual void OnCombatEnd() { }

    public class Factory(DalamudServices dalamud, EcsContainer ecsContainer, AttackManager attackManager, ILogger logger)
    {
        public T Create<T>() where T : Mechanic, new()
        {
            var mechanic = new T();
            mechanic.Init(dalamud, ecsContainer, attackManager, logger);
            return mechanic;
        }
    }
}
