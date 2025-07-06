using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts;

public abstract class Mechanic()
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected DalamudServices Dalamud { get; private set; }
    protected AttackManager AttackManager { get; private set; }
    protected ILogger Logger { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private void Init(
        DalamudServices dalamud,
        AttackManager attackManager,
        ILogger logger)
    {
        this.AttackManager = attackManager;
        this.Dalamud = dalamud;
        this.Logger = logger;
    }

    public virtual void OnDirectorUpdate(DirectorUpdateCategory a3) { }

    public virtual void OnObjectCreation(nint newObjectPointer, IGameObject? newObject) { }

    public virtual void OnActionEffectEvent(ActionEffectSet set) { }

    public class Factory(DalamudServices dalamud, AttackManager attackManager, ILogger logger)
    {
        public T Create<T>() where T : Mechanic, new()
        {
            var mechanic = new T();
            mechanic.Init(dalamud, attackManager, logger);
            return mechanic;
        }
    }
}
