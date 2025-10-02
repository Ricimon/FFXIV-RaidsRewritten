using System.Collections.Generic;
using System.IO;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Models;

public class Chefbingus(DalamudServices dalamud) : IEntity
{
    public Entity Create(World world)
    {
        var carby = world.Entity()
            .Set(new Model(413))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(1.1f));

        var replacements = new Dictionary<string, string>
        {
            { "chara/monster/m7002/obj/body/b0001/model/m7002b0001.mdl", "m0001b0002.mdl" },
            { "chara/monster/m7002/obj/body/b0001/material/v0005/mt_m0001b0002_a.mtrl", "mt_m0001b0002_a.mtrl" },
            { "chara/monster/m7002/obj/body/b0001/material/v0005/mt_m0001b0002_b.mtrl", "mt_m0001b0002_b.mtrl" },
            { "chara/monster/m7002/obj/body/b0001/material/v0005/mt_m0001b0002_c.mtrl", "mt_m0001b0002_c.mtrl" },
            { "chara/monster/m7002/obj/body/b0001/material/v0005/mt_m0001b0002_d.mtrl", "mt_m0001b0002_d.mtrl" },
            { "chara/monster/m7002/obj/body/b0001/material/v0005/mt_m0001b0002_e.mtrl", "mt_m0001b0002_e.mtrl" },
            { "chara/monster/m0001/obj/body/b0002/texture/unknown_n_359651549.tex", "unknown_n_359651549.tex" },
            { "chara/monster/m0001/obj/body/b0002/texture/unknown_m_359651549.tex", "unknown_m_359651549.tex" },
            { "chara/monster/m0001/obj/body/b0002/texture/unknown_id_359651549.tex", "unknown_id_359651549.tex" },
            // Sparkles
            { "chara/monster/m7002/obj/body/b0001/vfx/eff/vm0001.avfx", "" },
        };

        foreach (var r in replacements)
        {
            var replacementPath = Path.Combine("chefbingus", r.Value);
            replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
            world.Entity()
                .Set(new FileReplacement(r.Key, replacementPath))
                .ChildOf(carby);
        }

        return carby;
    }
}
