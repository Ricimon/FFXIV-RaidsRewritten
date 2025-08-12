// Adapted from https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/KeybindManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using RaidsRewritten.Extensions;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;

namespace RaidsRewritten.Interop;

internal enum KeyboardSource
{
    Game,
    ImGui,
}

public unsafe sealed class KeybindManager : IDisposable
{
    public bool InterceptMovementKeys { get; set; }

    private static readonly List<string> KeybindsToIntercept = [
        //"MOVE_STRIFE_L",
        //"MOVE_STRIFE_R",
        "JUMP",
        //"WALK",
        ];

    //private static string AutorunKeybind = "AUTORUN_KEY";

    // List of keys that can be used as a part of keybinds while the chat is
    // focused WITHOUT modifiers. All other keys can only be used if their
    // configured keybind contains modifiers (except only SHIFT). This allows
    // for using e.g. F11 to change chat channel while typing.
    private static readonly IReadOnlyCollection<VirtualKey> ModifierlessChatKeys = new[]
    {
        // VirtualKey.NO_KEY,
        // VirtualKey.LBUTTON,
        // VirtualKey.RBUTTON,
        // VirtualKey.CANCEL,
        // VirtualKey.MBUTTON,
        // VirtualKey.XBUTTON1,
        // VirtualKey.XBUTTON2,
        // VirtualKey.BACK,
        // VirtualKey.TAB, // handled by ChatLogWindow
        // VirtualKey.CLEAR,
        // VirtualKey.RETURN, // handled by imgui
        // VirtualKey.SHIFT,
        // VirtualKey.CONTROL,
        // VirtualKey.MENU,
        VirtualKey.PAUSE,
        // VirtualKey.CAPITAL,
        // VirtualKey.KANA,
        // VirtualKey.HANGUL,
        // VirtualKey.JUNJA,
        // VirtualKey.FINAL,
        // VirtualKey.HANJA,
        // VirtualKey.KANJI,
        VirtualKey.ESCAPE,
        // VirtualKey.CONVERT,
        // VirtualKey.NONCONVERT,
        // VirtualKey.ACCEPT,
        // VirtualKey.MODECHANGE,
        // VirtualKey.SPACE,
        VirtualKey.PRIOR,
        VirtualKey.NEXT,
        // VirtualKey.END,
        // VirtualKey.HOME,
        // VirtualKey.LEFT,  // handled by imgui
        // VirtualKey.UP,    // handled by ChatLogWindow
        // VirtualKey.RIGHT, // handled by imgui
        // VirtualKey.DOWN,  // handled by ChatLogWindow
        // VirtualKey.SELECT,
        VirtualKey.PRINT,
        VirtualKey.EXECUTE,
        VirtualKey.SNAPSHOT,
        // VirtualKey.INSERT,
        // VirtualKey.DELETE,
        VirtualKey.HELP,
        // VirtualKey.KEY_0,
        // VirtualKey.KEY_1,
        // VirtualKey.KEY_2,
        // VirtualKey.KEY_3,
        // VirtualKey.KEY_4,
        // VirtualKey.KEY_5,
        // VirtualKey.KEY_6,
        // VirtualKey.KEY_7,
        // VirtualKey.KEY_8,
        // VirtualKey.KEY_9,
        // VirtualKey.A,
        // VirtualKey.B,
        // VirtualKey.C,
        // VirtualKey.D,
        // VirtualKey.E,
        // VirtualKey.F,
        // VirtualKey.G,
        // VirtualKey.H,
        // VirtualKey.I,
        // VirtualKey.J,
        // VirtualKey.K,
        // VirtualKey.L,
        // VirtualKey.M,
        // VirtualKey.N,
        // VirtualKey.O,
        // VirtualKey.P,
        // VirtualKey.Q,
        // VirtualKey.R,
        // VirtualKey.S,
        // VirtualKey.T,
        // VirtualKey.U,
        // VirtualKey.V,
        // VirtualKey.W,
        // VirtualKey.X,
        // VirtualKey.Y,
        // VirtualKey.Z,
        // VirtualKey.LWIN,
        // VirtualKey.RWIN,
        VirtualKey.APPS,
        VirtualKey.SLEEP,
        // VirtualKey.NUMPAD0,
        // VirtualKey.NUMPAD1,
        // VirtualKey.NUMPAD2,
        // VirtualKey.NUMPAD3,
        // VirtualKey.NUMPAD4,
        // VirtualKey.NUMPAD5,
        // VirtualKey.NUMPAD6,
        // VirtualKey.NUMPAD7,
        // VirtualKey.NUMPAD8,
        // VirtualKey.NUMPAD9,
        // VirtualKey.MULTIPLY,
        // VirtualKey.ADD,
        // VirtualKey.SEPARATOR,
        // VirtualKey.SUBTRACT,
        // VirtualKey.DECIMAL,
        // VirtualKey.DIVIDE,
        VirtualKey.F1,
        VirtualKey.F2,
        VirtualKey.F3,
        VirtualKey.F4,
        VirtualKey.F5,
        VirtualKey.F6,
        VirtualKey.F7,
        VirtualKey.F8,
        VirtualKey.F9,
        VirtualKey.F10,
        VirtualKey.F11,
        VirtualKey.F12,
        VirtualKey.F13,
        VirtualKey.F14,
        VirtualKey.F15,
        VirtualKey.F16,
        VirtualKey.F17,
        VirtualKey.F18,
        VirtualKey.F19,
        VirtualKey.F20,
        VirtualKey.F21,
        VirtualKey.F22,
        VirtualKey.F23,
        VirtualKey.F24,
        // VirtualKey.NUMLOCK,
        // VirtualKey.SCROLL,
        // VirtualKey.OEM_FJ_JISHO,
        // VirtualKey.OEM_NEC_EQUAL,
        // VirtualKey.OEM_FJ_MASSHOU,
        // VirtualKey.OEM_FJ_TOUROKU,
        // VirtualKey.OEM_FJ_LOYA,
        // VirtualKey.OEM_FJ_ROYA,
        // VirtualKey.LSHIFT,
        // VirtualKey.RSHIFT,
        // VirtualKey.LCONTROL,
        // VirtualKey.RCONTROL,
        // VirtualKey.LMENU,
        // VirtualKey.RMENU,
        VirtualKey.BROWSER_BACK,
        VirtualKey.BROWSER_FORWARD,
        VirtualKey.BROWSER_REFRESH,
        VirtualKey.BROWSER_STOP,
        VirtualKey.BROWSER_SEARCH,
        VirtualKey.BROWSER_FAVORITES,
        VirtualKey.BROWSER_HOME,
        VirtualKey.VOLUME_MUTE,
        VirtualKey.VOLUME_DOWN,
        VirtualKey.VOLUME_UP,
        VirtualKey.MEDIA_NEXT_TRACK,
        VirtualKey.MEDIA_PREV_TRACK,
        VirtualKey.MEDIA_STOP,
        VirtualKey.MEDIA_PLAY_PAUSE,
        VirtualKey.LAUNCH_MAIL,
        VirtualKey.LAUNCH_MEDIA_SELECT,
        VirtualKey.LAUNCH_APP1,
        VirtualKey.LAUNCH_APP2,
        // VirtualKey.OEM_1,
        // VirtualKey.OEM_PLUS,
        // VirtualKey.OEM_COMMA,
        // VirtualKey.OEM_MINUS,
        // VirtualKey.OEM_PERIOD,
        // VirtualKey.OEM_2,
        // VirtualKey.OEM_3,
        // VirtualKey.OEM_4, // [{
        // VirtualKey.OEM_5, // \"
        // VirtualKey.OEM_6, // ]}
        // VirtualKey.OEM_7, // '"
        // VirtualKey.OEM_8,
        // VirtualKey.OEM_AX,
        // VirtualKey.OEM_102,
        // VirtualKey.ICO_HELP,
        // VirtualKey.ICO_00,
        // VirtualKey.PROCESSKEY,
        // VirtualKey.ICO_CLEAR,
        // VirtualKey.PACKET,
        // VirtualKey.OEM_RESET,
        // VirtualKey.OEM_JUMP,
        // VirtualKey.OEM_PA1,
        // VirtualKey.OEM_PA2,
        // VirtualKey.OEM_PA3,
        // VirtualKey.OEM_WSCTRL,
        // VirtualKey.OEM_CUSEL,
        // VirtualKey.OEM_ATTN,
        // VirtualKey.OEM_FINISH,
        // VirtualKey.OEM_COPY,
        // VirtualKey.OEM_AUTO,
        // VirtualKey.OEM_ENLW,
        // VirtualKey.OEM_BACKTAB,
        // VirtualKey.ATTN,
        // VirtualKey.CRSEL,
        // VirtualKey.EXSEL,
        // VirtualKey.EREOF,
        // VirtualKey.PLAY,
        // VirtualKey.ZOOM,
        // VirtualKey.NONAME,
        // VirtualKey.PA1,
        // VirtualKey.OEM_CLEAR,
    };

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private readonly Dictionary<string, Keybind> keybinds = [];

    //private Keybind? autorunKeybind;
    private long lastRefresh = long.MinValue;
    private bool vanillaTextInputHasFocus;

    public KeybindManager(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        this.dalamud.Framework.Update += HandleKeybinds;
    }

    public void Dispose()
    {
        this.dalamud.Framework.Update -= HandleKeybinds;
    }

    private void HandleKeybinds(IFramework framework)
    {
        // Refresh current keybinds every 5s
        if (lastRefresh + 5 * 1000 < Environment.TickCount64)
        {
            UpdateKeybinds();
            lastRefresh = Environment.TickCount64;
        }

        if (!InterceptMovementKeys) { return; }

        // Vanilla text input has focus
        if (RaptureAtkModule.Instance()->AtkModule.IsTextInputActive())
        {
            vanillaTextInputHasFocus = true;
            return;
        }

        // If the vanilla text input has just lost focus, clear all non-modifier
        // keys so we don't try to process them immediately on the next frame.
        if (vanillaTextInputHasFocus)
        {
            foreach (var key in this.dalamud.KeyState.GetValidVirtualKeys())
                if (key is not VirtualKey.CONTROL and not VirtualKey.SHIFT and not VirtualKey.MENU)
                    this.dalamud.KeyState[key] = false;
            vanillaTextInputHasFocus = false;
            return;
        }

        // Special section for autorun
        // --- This doesn't work if the autorun keybind is on a mouse key ---
        //if (InputManager.IsAutoRunning())
        //{
        //    // Dalamud does not support pressing keys
        //    // Because of this, I don't know how to cancel auto-run, only how to suppress it from
        //    // starting when movement is intercepted.
        //    //if (this.autorunKeybind!.Key1 != VirtualKey.NO_KEY)
        //    //{
        //    //    this.dalamud.KeyState[this.autorunKeybind!.Key1] = true;
        //    //}
        //    //else if (this.autorunKeybind!.Key2 != VirtualKey.NO_KEY)
        //    //{
        //    //    this.dalamud.KeyState[this.autorunKeybind!.Key2] = true;
        //    //}
        //}
        //else
        //{
        //    if (this.autorunKeybind!.Key1 != VirtualKey.NO_KEY)
        //    {
        //        this.dalamud.KeyState[this.autorunKeybind!.Key1] = false;
        //    }
        //    if (this.autorunKeybind!.Key2 != VirtualKey.NO_KEY)
        //    {
        //        this.dalamud.KeyState[this.autorunKeybind!.Key2] = false;
        //    }
        //}

        var modifierState = GetModifiers(KeyboardSource.Game);

        // Only process the active combo with the most modifiers
        var currentBest = (VirtualKey.NO_KEY, "", 0);
        foreach(var (toIntercept, keybind) in this.keybinds)
        {
            void Intercept(VirtualKey vk, Structs.ModifierFlag modifier)
            {
                if (!ComboPressed(KeyboardSource.Game, vk, modifier, modifierState: modifierState, false))
                    return;

                var bits = BitOperations.PopCount((uint)modifier);
                if (bits < currentBest.Item3)
                    return;

                currentBest = (vk, toIntercept, bits);
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        if (currentBest.Item1 == VirtualKey.NO_KEY)
            return;

        this.dalamud.KeyState[currentBest.Item1] = false;
    }

    private void UpdateKeybinds()
    {
        foreach(var name in KeybindsToIntercept)
        {
            this.keybinds[name] = GetKeybind(name);
        }
        //this.autorunKeybind = GetKeybind(AutorunKeybind);
    }

    private Structs.ModifierFlag GetModifiers(KeyboardSource source)
    {
        var modifierState = Structs.ModifierFlag.None;
        if (source == KeyboardSource.Game)
        {
            if (this.dalamud.KeyState[VirtualKey.MENU])
                modifierState |= Structs.ModifierFlag.Alt;
            if (this.dalamud.KeyState[VirtualKey.CONTROL])
                modifierState |= Structs.ModifierFlag.Ctrl;
            if (this.dalamud.KeyState[VirtualKey.SHIFT])
                modifierState |= Structs.ModifierFlag.Shift;
            return modifierState;
        }

        if (ImGui.GetIO().KeyAlt)
            modifierState |= Structs.ModifierFlag.Alt;
        if (ImGui.GetIO().KeyCtrl)
            modifierState |= Structs.ModifierFlag.Ctrl;
        if (ImGui.GetIO().KeyShift)
            modifierState |= Structs.ModifierFlag.Shift;

        return modifierState;
    }

    private bool ComboPressed(KeyboardSource source, VirtualKey key, Structs.ModifierFlag modifier, Structs.ModifierFlag? modifierState = null, bool modifiersOnly = false)
    {
        // When we're in an input, we don't want to process any keybinds that
        // don't have a modifier (or only use shift) and are not explicitly
        // whitelisted.
        if (modifiersOnly && !ModifierlessChatKeys.Contains(key) && modifier is Structs.ModifierFlag.None or Structs.ModifierFlag.Shift)
            return false;

        modifierState ??= GetModifiers(source);
        var modifierPressed = modifierState.Value.HasFlag(modifier);

        return KeyPressed(source, key) && modifierPressed;
    }

    private bool KeyPressed(KeyboardSource source, VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY)
            return false;

        if (!this.dalamud.KeyState.IsVirtualKeyValid(key))
            return false;

        if (source == KeyboardSource.Game)
            return this.dalamud.KeyState[key];

        return key.TryToImGui(out var imguiKey) && ImGui.IsKeyPressed(imguiKey);
    }

    private static Keybind GetKeybind(string id)
    {
        var outData = new UIInputData.Keybind();
        var idString = Utf8String.FromString(id);
        UIInputData.Instance()->GetKeybind(idString, &outData);
        idString->Dtor(true);

        var key1 = RemapInvalidVirtualKey((VirtualKey)outData.Key);
        var key2 = RemapInvalidVirtualKey((VirtualKey)outData.AltKey);
        return new Keybind
        {
            Key1 = key1,
            Modifier1 = (Structs.ModifierFlag)outData.Modifier,
            Key2 = key2,
            Modifier2 = (Structs.ModifierFlag)outData.AltModifier,
        };
    }

    private static VirtualKey RemapInvalidVirtualKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.F23 => VirtualKey.OEM_2,     // /?
            (VirtualKey)140 => VirtualKey.OEM_7,    // '"
            _ => key
        };
    }
}
