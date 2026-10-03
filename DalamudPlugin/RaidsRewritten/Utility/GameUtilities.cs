using System.Text.RegularExpressions;

namespace RaidsRewritten.Utility;

public static partial class GameUtilities
{
    public static bool IsValidVfxPath(string path)
    {
        return VfxRegex().IsMatch(path);
    }

    public static bool IsValidSfxPath(string path)
    {
        return SfxRegex().IsMatch(path);
    }

    [GeneratedRegex(@"(^vfx|^bg|^bgcommon)\/[\w\/]*\w+\.avfx$")]
    private static partial Regex VfxRegex();
    [GeneratedRegex(@"^sound\/(vfx|battle)\/[\w\/]*\w+\.scd$")]
    private static partial Regex SfxRegex();
}
