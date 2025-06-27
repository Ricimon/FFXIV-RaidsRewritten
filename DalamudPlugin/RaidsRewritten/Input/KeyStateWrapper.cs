using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace RaidsRewritten.Input;

public class KeyStateWrapper : IKeyState, IDisposable
{
    public event Action<VirtualKey>? OnKeyUp;
    public event Action<VirtualKey>? OnKeyDown;

    private readonly IKeyState keyState;
    private readonly IFramework framework;

    private readonly Dictionary<VirtualKey, bool> keyStates = [];

    public bool this[VirtualKey vkCode] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public bool this[int vkCode] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public KeyStateWrapper(
        IKeyState keyState,
        IFramework framework)
    {
        this.keyState = keyState;
        this.framework = framework;

        this.framework.Update += OnFrameworkUpdate;
    }

    public int GetRawValue(int vkCode)
    {
        return this.keyState.GetRawValue(vkCode);
    }

    public int GetRawValue(VirtualKey vkCode)
    {
        return this.keyState.GetRawValue(vkCode);
    }

    public void SetRawValue(int vkCode, int value)
    {
        this.keyState.SetRawValue(vkCode, value);
    }

    public void SetRawValue(VirtualKey vkCode, int value)
    {
        this.keyState.SetRawValue(vkCode, value);
    }

    public bool IsVirtualKeyValid(int vkCode)
    {
        return this.keyState.IsVirtualKeyValid(vkCode);
    }

    public bool IsVirtualKeyValid(VirtualKey vkCode)
    {
        return this.keyState.IsVirtualKeyValid(vkCode);
    }

    public IEnumerable<VirtualKey> GetValidVirtualKeys()
    {
        return this.keyState.GetValidVirtualKeys();
    }

    public void ClearAll()
    {
        this.keyState.ClearAll();
    }

    public void Dispose()
    {
        this.framework.Update -= OnFrameworkUpdate;
        GC.SuppressFinalize(this);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        foreach (var key in this.keyState.GetValidVirtualKeys())
        {
            var keyState = this.keyState.GetRawValue(key) != 0;
            if (!this.keyStates.TryGetValue(key, out var oldState))
            {
                this.keyStates.Add(key, keyState);
            }
            else
            {
                if (oldState != keyState)
                {
                    this.keyStates[key] = keyState;
                    if (keyState)
                    {
                        this.OnKeyDown?.Invoke(key);
                    }
                    else
                    {
                        this.OnKeyUp?.Invoke(key);
                    }
                }
            }
        }
    }
}
