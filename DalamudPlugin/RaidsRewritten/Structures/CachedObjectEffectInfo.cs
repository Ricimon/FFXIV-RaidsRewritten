// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Structures/CachedObjectEffectInfo.cs
// 0054cc3
using System;

namespace RaidsRewritten.Structures;

public record struct CachedObjectEffectInfo
{
    public long StartTime;
    public ushort data1;
    public ushort data2;

    public CachedObjectEffectInfo(long startTime, ushort data1, ushort data2)
    {
        StartTime = startTime;
        this.data1 = data1;
        this.data2 = data2;
    }

    public float StartTimeF
    {
        get
        {
            return (float)StartTime / 1000f;
        }
    }

    public long Age
    {
        get
        {
            return Environment.TickCount64 - StartTime;
        }
    }

    public float AgeF
    {
        get
        {
            return (float)Age / 1000f;
        }
    }
}