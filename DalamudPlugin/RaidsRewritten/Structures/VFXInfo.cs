// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Structures/VFXInfo.cs
// 470c334
using System;

namespace RaidsRewritten.Structures;

public record struct VFXInfo
{
    public long SpawnTime;

    public long Age
    {
        get
        {
            return Environment.TickCount64 - SpawnTime;
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