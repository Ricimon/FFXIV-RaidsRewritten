using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Events;
using WindowsInput.Events.Sources;

namespace RaidsRewritten.Input;

public sealed class InputEventSource : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

    private static bool IsGameFocused()
    {
        try
        {
            var foregroundWindowHandle = GetForegroundWindow();
            if (foregroundWindowHandle == IntPtr.Zero) return false;

            _ = GetWindowThreadProcessId(foregroundWindowHandle, out var activeProcessId);

            return activeProcessId == Environment.ProcessId;
        }
        catch (EntryPointNotFoundException)
        {
            return true;
        }
    }

    private readonly List<Action<KeyDown>> subscribedKeyDownActions = [];
    private readonly List<Action<KeyUp>> subscribedKeyUpActions = [];

    private IKeyboardEventSource? keyboard;
    private IMouseEventSource? mouse;

    public void Dispose()
    {
        if (keyboard != null)
        {
            keyboard.KeyDown -= OnKeyboardKeyDown;
            keyboard.KeyUp -= OnKeyboardKeyUp;
        }
        keyboard?.Dispose();
        if (mouse != null)
        {
            mouse.ButtonDown -= OnMouseButtonDown;
            mouse.ButtonUp -= OnMouseButtonUp;
        }
        mouse?.Dispose();
    }

    public void SubscribeToKeyDown(Action<KeyDown> action)
    {
        // One-time setup of persistent listeners
        SetupGlobalSubscriptions();

        subscribedKeyDownActions.Add(action);
    }

    public void UnsubscribeToKeyDown(Action<KeyDown> action)
    {
        subscribedKeyDownActions.Remove(action);
    }

    public void SubscribeToKeyUp(Action<KeyUp> action)
    {
        // One-time setup of persistent listeners
        SetupGlobalSubscriptions();

        subscribedKeyUpActions.Add(action);
    }

    public void UnsubscribeToKeyUp(Action<KeyUp> action)
    {
        subscribedKeyUpActions.Remove(action);
    }

    private void SetupGlobalSubscriptions()
    {
        if (keyboard == null)
        {
            keyboard = Capture.Global.KeyboardAsync();
            keyboard.KeyDown += OnKeyboardKeyDown;
            keyboard.KeyUp += OnKeyboardKeyUp;
        }
        if (mouse == null)
        {
            mouse = Capture.Global.MouseAsync();
            mouse.ButtonDown += OnMouseButtonDown;
            mouse.ButtonUp += OnMouseButtonUp;
        }
    }

    private void OnKeyboardKeyDown(object? o, EventSourceEventArgs<KeyDown> e)
    {
        foreach (var action in subscribedKeyDownActions)
        {
            action.Invoke(e.Data);
        }
    }

    private void OnKeyboardKeyUp(object? o, EventSourceEventArgs<KeyUp> e)
    {
        // Always listen to key ups, since these are necessary to cancel hold actions
        foreach (var action in subscribedKeyUpActions)
        {
            action.Invoke(e.Data);
        }
    }

    private void OnMouseButtonDown(object? o, EventSourceEventArgs<ButtonDown> e)
    {
        KeyCode keyCode;
        switch(e.Data.Button)
        {
            case ButtonCode.Left:
                keyCode = KeyCode.LButton; break;
            case ButtonCode.Right:
                keyCode = KeyCode.RButton; break;
            case ButtonCode.Middle:
                keyCode = KeyCode.MButton; break;
            case ButtonCode.XButton1:
                keyCode = KeyCode.XButton1; break;
            case ButtonCode.XButton2:
                keyCode = KeyCode.XButton2; break;
            default:
                return;
        }

        foreach (var action in subscribedKeyDownActions)
        {
            action.Invoke(new(keyCode));
        }
    }

    private void OnMouseButtonUp(object? o, EventSourceEventArgs<ButtonUp> e)
    {
        // Always listen to key ups, since these are necessary to cancel hold actions
        KeyCode keyCode;
        switch(e.Data.Button)
        {
            case ButtonCode.Left:
                keyCode = KeyCode.LButton; break;
            case ButtonCode.Right:
                keyCode = KeyCode.RButton; break;
            case ButtonCode.Middle:
                keyCode = KeyCode.MButton; break;
            case ButtonCode.XButton1:
                keyCode = KeyCode.XButton1; break;
            case ButtonCode.XButton2:
                keyCode = KeyCode.XButton2; break;
            default:
                return;
        }

        foreach (var action in subscribedKeyUpActions)
        {
            action.Invoke(new(keyCode));
        }
    }
}
