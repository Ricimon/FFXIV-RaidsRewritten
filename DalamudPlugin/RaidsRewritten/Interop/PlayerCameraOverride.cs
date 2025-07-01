using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;

namespace RaidsRewritten.Interop;

public unsafe sealed class PlayerCameraOverride : IDisposable
{
    public bool Enabled
    {
        get => this._rmiCameraHook.IsEnabled;
        set
        {
            if (value)
            {
                this._rmiCameraHook.Enable();
            }
            else
            {
                this._rmiCameraHook.Disable();
            }
        }
    }

    public Angle DesiredAzimuth;
    //public Angle DesiredAltitude;
    public Angle SpeedH = 720.Degrees(); // per second
    //public Angle SpeedV = 360.Degrees(); // per second

    private delegate void RMICameraDelegate(CameraEx* self, int inputMode, float speedH, float speedV);
    [Signature("E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 44 0F 28 4C 24 ??")]
    private Hook<RMICameraDelegate> _rmiCameraHook = null!;

    private readonly ILogger logger;

    public PlayerCameraOverride(DalamudServices dalamud, ILogger logger)
    {
        this.logger = logger;
        dalamud.GameInteropProvider.InitializeFromAttributes(this);
    }

    public void Dispose()
    {
        this._rmiCameraHook?.Dispose();
    }

    private void RMICameraDetour(CameraEx* self, int inputMode, float speedH, float speedV)
    {
        _rmiCameraHook.Original(self, inputMode, speedH, speedV);
        // Special check specifically meant to enforce forced-movement
        // There really shouldn't be another reason to force camera direction...
        if (InputManager.IsAutoRunning())
        {
            var dt = Framework.Instance()->FrameDeltaTime;
            var deltaH = (DesiredAzimuth - self->DirH.Radians()).Normalized();
            //var deltaV = (DesiredAltitude - self->DirV.Radians()).Normalized();
            var maxH = SpeedH.Rad * dt;
            //var maxV = SpeedV.Rad * dt;
            self->InputDeltaH = Math.Clamp(deltaH.Rad, -maxH, maxH);
            //self->InputDeltaV = Math.Clamp(deltaV.Rad, -maxV, maxV);
        }
    }
}
