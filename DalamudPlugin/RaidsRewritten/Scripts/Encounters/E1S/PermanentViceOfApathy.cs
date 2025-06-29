using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class PermanentViceOfApathy : Mechanic
{
    private const uint VICE_OF_APATHY_DATA_ID = 0x1EAE20;

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != VICE_OF_APATHY_DATA_ID) { return; }

        this.Logger.Info("Found Vice of Apathy");

        //var vfxPath = "bgcommon/world/common/vfx_for_btl/b0222/eff/b0222_twis_y.avfx";
        //this.VfxSpawn.SpawnGroundVfx(vfxPath, newObject.Position, newObject.Rotation);
    }
}
