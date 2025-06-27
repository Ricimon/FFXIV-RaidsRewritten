using System;

namespace RaidsRewritten;

public interface IDalamudHook : IDisposable
{
    void HookToDalamud();
}
