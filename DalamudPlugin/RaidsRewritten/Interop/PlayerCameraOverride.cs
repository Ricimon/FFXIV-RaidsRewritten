// Adapted from https://github.com/awgil/ffxiv_navmesh/blob/master/vnavmesh/Movement/OverrideCamera.cs
using System;
using Dalamud.Game.Config;
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
    [Signature("48 8B C4 53 48 81 EC ?? ?? ?? ?? 44 0F 29 50 ??")]
    private Hook<RMICameraDelegate> _rmiCameraHook = null!;

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private bool legacyMode;

    public PlayerCameraOverride(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        dalamud.GameInteropProvider.InitializeFromAttributes(this);
        this.dalamud.GameConfig.UiControlChanged += OnConfigChanged;
        UpdateLegacyMode();
    }

    public void Dispose()
    {
        this.dalamud.GameConfig.UiControlChanged -= OnConfigChanged;
        this._rmiCameraHook.Dispose();
    }

    private void RMICameraDetour(CameraEx* self, int inputMode, float speedH, float speedV)
    {
        _rmiCameraHook.Original(self, inputMode, speedH, speedV);
        // Special check specifically meant to enforce forced-movement
        // There really shouldn't be another reason to force camera direction...
        // Standard mode L: camera direction must be forced or else the character can be seen moonwalking
        if (!this.legacyMode || InputManager.IsAutoRunning())
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

    private void OnConfigChanged(object? sender, ConfigChangeEvent evt) => UpdateLegacyMode();
    private void UpdateLegacyMode()
    {
        this.legacyMode = this.dalamud.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
    }
}
