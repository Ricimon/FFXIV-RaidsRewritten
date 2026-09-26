using System.Collections.Generic;
using System.IO;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Models;

public class Gear(DalamudServices dalamud) : IEntity
{
    public Entity Create(World world)
    {
        var gear = world.Entity()
            .Set(new Model(4714))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(1.0f))
            .Set(new ModelTimelineSpeed(0));

        var replacements = new Dictionary<string, string>
        {
            { "chara/monster/m0315/obj/body/b0004/model/m0315b0004.mdl", "gear.mdl" },
            { "chara/monster/m0315/obj/body/b0004/material/v0002/mt_m0315b0004_a.mtrl", "gear.mtrl" },
            { "chara/monster/m0315/obj/body/b0004/texture/v02_m0315b0004_base.tex", "gear_d.tex" },
            { "chara/monster/m0315/obj/body/b0004/texture/v02_m0315b0004_id.tex", "gear_id.tex" },
            { "chara/monster/m0315/obj/body/b0004/texture/v02_m0315b0004_mask.tex", "gear_mask.tex" },
            { "chara/monster/m0315/obj/body/b0004/texture/v02_m0315b0004_norm.tex", "gear_n.tex" },
        };

        foreach (var r in replacements)
        {
            var replacementPath = Path.Combine("models", "gear", r.Value);
            replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
            world.Entity()
                .Set(new FileReplacement(r.Key, replacementPath))
                .ChildOf(gear);
        }

        return gear;
    }
}
