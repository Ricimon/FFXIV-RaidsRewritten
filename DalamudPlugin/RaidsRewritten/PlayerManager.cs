using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Config;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;

namespace RaidsRewritten;

public unsafe class PlayerManager : IDisposable
{
    public bool OverrideMovement
    {
        get => _rmiWalkHook.IsEnabled;
        set
        {
            if (value)
            {
                _rmiWalkHook.Enable();
            }
            else
            {
                _rmiWalkHook.Disable();
            }
        }
    }
    public Vector3 OverrideMovementDirection { get; set; }

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private delegate bool RMIWalkIsInputEnabled(void* self);
    private RMIWalkIsInputEnabled _rmiWalkIsInputEnabled1;
    private RMIWalkIsInputEnabled _rmiWalkIsInputEnabled2;

    private delegate void RMIWalkDelegate(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);
    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D")]
    private Hook<RMIWalkDelegate> _rmiWalkHook = null!;

    private bool legacyMode;

    public PlayerManager(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        var rmiWalkIsInputEnabled1Addr = dalamud.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C");
        var rmiWalkIsInputEnabled2Addr = dalamud.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F");
        _rmiWalkIsInputEnabled1 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled1Addr);
        _rmiWalkIsInputEnabled2 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled2Addr);

        dalamud.GameInteropProvider.InitializeFromAttributes(this);
        this.dalamud.GameConfig.UiControlChanged += OnConfigChanged;
        UpdateLegacyMode();
    }

    public void Dispose()
    {
        this.dalamud.GameConfig.UiControlChanged -= OnConfigChanged;
        _rmiWalkHook.Dispose();
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        _rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // TODO: we really need to introduce some extra checks that PlayerMoveController::readInput does - sometimes it skips reading input, and returning something non-zero breaks stuff...
        bool movementAllowed = bAdditiveUnk == 0 && _rmiWalkIsInputEnabled1(self) && _rmiWalkIsInputEnabled2(self); //&& !Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BeingMoved];
        //UserInput = *sumLeft != 0 || *sumForward != 0;
        if (movementAllowed && GetDirectionAngles(false) is var relDir && relDir != null)
        {
            var dir = relDir.Value.h.ToDirection();
            *sumLeft = dir.X;
            *sumForward = dir.Y;
        }
    }

    private (Angle h, Angle v)? GetDirectionAngles(bool allowVertical)
    {
        var player = this.dalamud.ClientState.LocalPlayer;
        if (player == null) { return null; }

        if (this.OverrideMovementDirection == Vector3.Zero) { return null; }

        var dirH = Angle.FromDirectionXZ(this.OverrideMovementDirection);
        var dirV = allowVertical ? Angle.FromDirection(new(this.OverrideMovementDirection.Y, this.OverrideMovementDirection.ToVector2().Length())) : default;

        var refDir = this.legacyMode
            ? ((CameraEx*)CameraManager.Instance()->GetActiveCamera())->DirH.Radians() + 180.Degrees()
            : player.Rotation.Radians();
        return (dirH - refDir, dirV);
            
    }

    private void OnConfigChanged(object? sender, ConfigChangeEvent evt) => UpdateLegacyMode();
    private void UpdateLegacyMode()
    {
        this.legacyMode = this.dalamud.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
    }
}
