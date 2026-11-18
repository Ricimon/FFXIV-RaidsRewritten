// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/BaseVfx.cs
// 3d7b0eb

namespace RaidsRewritten.Interop.Structs.Vfx;

public abstract class BaseVfx
{
    public string Path;

    public BaseVfx(string path)
    {
        Path = path;
    }

    public abstract void Remove();

    public abstract nint GetAddress();
}
