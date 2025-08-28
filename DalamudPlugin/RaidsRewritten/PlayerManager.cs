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

    public PlayerMovementOverride.OverrideMovementState OverrideMovement
    {
        get => movementOverride.OverrideMovement;
        set
        {
            movementOverride.OverrideMovement = value;

            cameraOverride.Enabled =
                movementOverride.OverrideMovement == PlayerMovementOverride.OverrideMovementState.ForceMovementWorldDirection
                    && movementOverride.OverrideMovementWorldDirection != Vector3.Zero;
        }
    }

    public Vector3 OverrideMovementWorldDirection
    {
        get => movementOverride.OverrideMovementWorldDirection;
        set
        {
            movementOverride.OverrideMovementWorldDirection = value;

            cameraOverride.Enabled =
                movementOverride.OverrideMovement == PlayerMovementOverride.OverrideMovementState.ForceMovementWorldDirection
                    && movementOverride.OverrideMovementWorldDirection != Vector3.Zero;
            cameraOverride.DesiredAzimuth = Angle.FromDirectionXZ(value) + 180.Degrees();
        }
    }

    public Vector2 OverrideMovementCameraDirection
    {
        get => movementOverride.OverrideMovementCameraDirection;
        set => movementOverride.OverrideMovementCameraDirection = value;
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
