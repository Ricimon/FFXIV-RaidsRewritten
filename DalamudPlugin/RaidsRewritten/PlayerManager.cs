using System.Numerics;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;

namespace RaidsRewritten;

public class PlayerManager(
    PlayerMovementOverride movementOverride,
    PlayerCameraOverride cameraOverride,
    ActionManagerEx actionManager,
    ILogger logger)
{
    public bool IsMovementAllowedByGame => movementOverride.IsMovementAllowedByGame;

    public bool OverrideMovement
    {
        get => movementOverride.OverrideMovement || cameraOverride.Enabled;
        set
        {
            movementOverride.OverrideMovement = value;

            cameraOverride.Enabled =
                movementOverride.OverrideMovement &&
                movementOverride.OverrideMovementDirection != Vector3.Zero;
        }
    }

    public Vector3 OverrideMovementDirection
    {
        get => movementOverride.OverrideMovementDirection;
        set
        {
            movementOverride.OverrideMovementDirection = value;

            cameraOverride.Enabled =
                movementOverride.OverrideMovement &&
                movementOverride.OverrideMovementDirection != Vector3.Zero;
            cameraOverride.DesiredAzimuth = Angle.FromDirectionXZ(value) + 180.Degrees();
        }
    }

    public PlayerMovementOverride.ForcedWalkState ForceWalk
    {
        get => movementOverride.ForceWalk;
        set => movementOverride.ForceWalk = value;
    }

    public bool DisableAllActions
    {
        get => actionManager.DisableAllActions;
        set
        {
            if (!DisableAllActions && value)
            {
                actionManager.CancelCast();
            }
            actionManager.DisableAllActions = value;
        }
    }
}
