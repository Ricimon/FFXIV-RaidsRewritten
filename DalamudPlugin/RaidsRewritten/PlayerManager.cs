using System.Numerics;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;

namespace RaidsRewritten;

public class PlayerManager
{
    public bool OverrideMovement
    {
        get => this.movementOverride.OverrideMovement || 
            this.cameraOverride.Enabled ||
            this.keybindManager.InterceptMovementKeys;
        set => this.movementOverride.OverrideMovement =
            this.cameraOverride.Enabled =
            this.keybindManager.InterceptMovementKeys = value;
    }

    public Vector3 OverrideMovementDirection
    {
        get => this.movementOverride.OverrideMovementDirection;
        set
        {
            this.movementOverride.OverrideMovementDirection = value;
            this.cameraOverride.DesiredAzimuth = Angle.FromDirectionXZ(value) + 180.Degrees();
        }
    }

    private readonly PlayerMovementOverride movementOverride;
    private readonly PlayerCameraOverride cameraOverride;
    private readonly KeybindManager keybindManager;
    private readonly ILogger logger;

    public PlayerManager(
        PlayerMovementOverride movementOverride,
        PlayerCameraOverride cameraOverride,
        KeybindManager keybindManager,
        ILogger logger)
    {
        this.movementOverride = movementOverride;
        this.cameraOverride = cameraOverride;
        this.keybindManager = keybindManager;
        this.logger = logger;
    }
}
