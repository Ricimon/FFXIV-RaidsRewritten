﻿using RaidsRewritten;
using System;

namespace ECommons.Hooks.ActionEffectTypes;
#nullable disable

public unsafe struct TargetEffect
{
    private readonly EffectEntry* _effects;

    public ulong TargetID { get; }

    public TargetEffect(ulong targetId, EffectEntry* effects)
    {
        TargetID = targetId;
        _effects = effects;
    }

    /// <summary>
    /// Get Effect.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public EffectEntry this[int index]
    {
        get
        {
            if(index < 0 || index > 7) return default;
            return _effects[index];
        }
    }

    public override string ToString()
    {
        var str = "Effect Target: " + PluginInitializer.ObjectTable.SearchById(TargetID)?.Name?.ToString();
        ForEach(e =>
        {
            if (e.type == ActionEffectType.Nothing) { return; }
            str += "\n    " + e.ToString();
        });
        return str;
    }

    public bool GetSpecificTypeEffect(ActionEffectType type, out EffectEntry effect)
    {
        var find = false;
        EffectEntry result = default;
        ForEach(e =>
        {
            if(!find && e.type == type)
            {
                find = true;
                result = e;
            }
        });
        effect = result;
        return find;
    }

    public void ForEach(Action<EffectEntry> act)
    {
        if(act == null) return;
        for(var i = 0; i < 8; i++)
        {
            var e = this[i];
            act(e);
        }
    }
}
