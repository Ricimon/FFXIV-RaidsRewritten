// Adapted from https://github.com/awgil/ffxiv_navmesh/blob/master/vnavmesh/Movement/OverrideMovement.cs
// and https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/SmartStrafe.cs
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Config;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;

namespace RaidsRewritten.Interop;

public unsafe sealed class PlayerMovementOverride : IDisposable
{
    public bool IsMovementAllowedByGame { get; private set; }

    public enum OverrideMovementState
    {
        None,
        ForceMovementWorldDirection,
        ForceMovementCameraDirection,
    }
    public OverrideMovementState OverrideMovement { get; set; }
    public Vector3 OverrideMovementWorldDirection { get; set; }
    public Vector2 OverrideMovementCameraDirection { get; set; }
    public enum ForcedWalkState
    {
        None,
        Walk,
        Run,
    }
    public ForcedWalkState ForceWalk { get; set; }

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private delegate bool RMIWalkIsInputEnabled(void* self);
    private RMIWalkIsInputEnabled rmiWalkIsInputEnabled1;
    private RMIWalkIsInputEnabled rmiWalkIsInputEnabled2;

    private delegate void RMIWalkDelegate(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);
    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D")]
    private Hook<RMIWalkDelegate> rmiWalkHook = null!;

    private enum KeybindType : int
    {
        MoveForward = 321,
        MoveBack = 322,
        TurnLeft = 323,
        TurnRight = 324,
        StrafeLeft = 325,
        StrafeRight = 326,
    }

    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool CheckStrafeKeybindDelegate(IntPtr ptr, KeybindType keybind);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 06 01 BA 44 01 00 00")]
    private Hook<CheckStrafeKeybindDelegate> checkStrafeKeybindHook = null!;

    private readonly Hook<InputData.Delegates.IsInputIdPressed> isInputIdPressedHook;

    private bool legacyMode;
    private bool forcedWalkStateLastFrame;
    private bool wasWalking;

    public PlayerMovementOverride(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        var rmiWalkIsInputEnabled1Addr = dalamud.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C");
        var rmiWalkIsInputEnabled2Addr = dalamud.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F");
        rmiWalkIsInputEnabled1 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled1Addr);
        rmiWalkIsInputEnabled2 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled2Addr);

        var hook = dalamud.GameInteropProvider;
        hook.InitializeFromAttributes(this);
        isInputIdPressedHook = hook.HookFromAddress<InputData.Delegates.IsInputIdPressed>(InputData.Addresses.IsInputIdPressed.Value, IsInputIdPressedDetour);

        dalamud.GameConfig.UiControlChanged += OnConfigChanged;
        UpdateLegacyMode();

        rmiWalkHook.Enable();
        checkStrafeKeybindHook.Enable();
        isInputIdPressedHook.Enable();
    }

    public void Dispose()
    {
        this.dalamud.GameConfig.UiControlChanged -= OnConfigChanged;
        rmiWalkHook.Dispose();
        checkStrafeKeybindHook.Dispose();
        isInputIdPressedHook.Dispose();
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // TODO: we really need to introduce some extra checks that PlayerMoveController::readInput does - sometimes it skips reading input, and returning something non-zero breaks stuff...
        //this.logger.Info($"WalkDetour rmi1:{rmiWalkIsInputEnabled1(self)}, rmi2: {rmiWalkIsInputEnabled2(self)}, a6: {*a6}, sumLeft:{*sumLeft}, sumForward:{*sumForward}, backOrStrafe:{*haveBackwardOrStrafe}, bAdd:{bAdditiveUnk}");

        // Walk save & restoration
        var forcedWalkState = ForceWalk != ForcedWalkState.None;
        if (forcedWalkState != forcedWalkStateLastFrame)
        {
            if (forcedWalkState)
            {
                wasWalking = Control.Instance()->IsWalking;
            }
            else
            {
                Control.Instance()->IsWalking = wasWalking;
            }
        }
        forcedWalkStateLastFrame = forcedWalkState;

        // Found through testing, this value is more reliable to determine if movement is locked due to being knocked back
        var isBeingKnockedBack = *(byte*)((IntPtr)self + 62) != 0;
        IsMovementAllowedByGame = bAdditiveUnk == 0 && rmiWalkIsInputEnabled1(self) && rmiWalkIsInputEnabled2(self) && !isBeingKnockedBack;
        //UserInput = *sumLeft != 0 || *sumForward != 0;

        var overrideMovement = OverrideMovement != OverrideMovementState.None;
        if (overrideMovement && IsMovementAllowedByGame)
        {
            if (OverrideMovement == OverrideMovementState.ForceMovementWorldDirection)
            {
                var relDir = GetDirectionAngles(false);
                if (relDir != null)
                {
                    var dir = relDir.Value.h.ToDirection();
                    *sumLeft = dir.X;
                    *sumForward = dir.Y;
                    *haveBackwardOrStrafe = 0;
                    Control.Instance()->IsWalking = false;
                }
                else
                {
                    *sumLeft = 0;
                    *sumForward = 0;
                }
            }
            else if (OverrideMovement == OverrideMovementState.ForceMovementCameraDirection)
            {
                *sumLeft = Math.Clamp(this.OverrideMovementCameraDirection.X, -1, 1);
                *sumForward = Math.Clamp(this.OverrideMovementCameraDirection.Y, -1, 1);
            }
        }

        if (forcedWalkState)
        {
            Control.Instance()->IsWalking = ForceWalk == ForcedWalkState.Walk;
        }
    }

    private bool CheckStrafeKeybind(IntPtr ptr, KeybindType keybind)
    {
        if (OverrideMovement != OverrideMovementState.None)
        {
            if (keybind == KeybindType.StrafeLeft || keybind == KeybindType.StrafeRight)
            { 
                // Disables strafing (but does not unset the input key)
                return false;
            }
        }
        return checkStrafeKeybindHook.Original(ptr, keybind);
    }

    private bool IsInputIdPressedDetour(InputData* inputData, InputId inputId)
    {
        if (OverrideMovement != OverrideMovementState.None)
        {
            if (inputId == InputId.JUMP ||
                inputId == InputId.PAD_JUMPANDCANCELCAST)
            {
                return false;
            }
        }
        return isInputIdPressedHook.Original(inputData, inputId);
    }

    private (Angle h, Angle v)? GetDirectionAngles(bool allowVertical)
    {
        var player = this.dalamud.ClientState.LocalPlayer;
        if (player == null) { return null; }

        if (this.OverrideMovementWorldDirection == Vector3.Zero) { return null; }

        var dirH = Angle.FromDirectionXZ(this.OverrideMovementWorldDirection);
        var dirV = allowVertical ? Angle.FromDirection(new(this.OverrideMovementWorldDirection.Y, this.OverrideMovementWorldDirection.ToVector2().Length())) : default;

        var refDir = this.legacyMode && !InputManager.IsAutoRunning()
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
