using Dalamud.Game.ClientState.Objects.Types;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts;

public abstract class Mechanic()
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected VfxSpawn VfxSpawn { get; private set; }
    protected DalamudServices Dalamud { get; private set; }
    protected ILogger Logger { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private void Init(
        VfxSpawn vfxSpawn,
        DalamudServices dalamud,
        ILogger logger)
    {
        this.VfxSpawn = vfxSpawn;
        this.Dalamud = dalamud;
        this.Logger = logger;
    }

    public abstract void OnObjectCreation( nint newObjectPointer, IGameObject? newObject);

    public class Factory(VfxSpawn vfxSpawn, DalamudServices dalamud, ILogger logger)
    {
        public T Create<T>() where T : Mechanic, new()
        {
            var mechanic = new T();
            mechanic.Init(vfxSpawn, dalamud, logger);
            return mechanic;
        }
    }
}
