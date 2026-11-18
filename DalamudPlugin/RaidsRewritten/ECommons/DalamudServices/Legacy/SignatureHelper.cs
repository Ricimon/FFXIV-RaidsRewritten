// https://github.com/NightmareXIV/ECommons/blob/master/ECommons/DalamudServices/Legacy/SignatureHelper.cs
// 3121eb8
namespace ECommons.DalamudServices.Legacy;

public static class SignatureHelper
{
    public static void Initialise(object which, bool log = false)
    {
        Svc.Hook.InitializeFromAttributes(which);
    }
}
