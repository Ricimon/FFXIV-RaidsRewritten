using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Timer;

namespace RaidsRewritten.Utility;

public static class FFXIVClientStructsExtensions
{
    public static bool IsValid(this HouseId houseId)
    {
        return houseId != ulong.MaxValue;
    }

    public static TimeSpan GetEorzeaTimeOfDay(this ClientTime clientTime)
    {
        var et = clientTime.EorzeaTime;
        et %= 86400; // seconds in a day
        return TimeSpan.FromSeconds(et);
    }
}
