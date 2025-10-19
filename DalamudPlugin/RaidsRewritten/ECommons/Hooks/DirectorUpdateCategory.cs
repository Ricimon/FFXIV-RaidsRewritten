// https://github.com/NightmareXIV/ECommons/blob/master/ECommons/Hooks/DirectorUpdateCategory.cs
// 3121eb8
namespace ECommons.Hooks;

public enum DirectorUpdateCategory : uint
{
    Commence = 0x40000001,
    Recommence = 0x40000006,
    Complete = 0x40000003,
    Wipe = 0x40000005
}
