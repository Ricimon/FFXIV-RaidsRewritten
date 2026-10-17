using System.Collections.Generic;
using System.IO;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Models;

public class Bolt(DalamudServices dalamud) : IEntity
{
    public Entity Create(World world)
    {
        var bolt = world.Entity()
            .Set(new Model(221))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(1.0f))
            .Set(new ModelTimelineSpeed(0));

        var replacements = new Dictionary<string, string>
        {
            { "chara/monster/m0059/obj/body/b0001/model/m0059b0001.mdl", "bolt.mdl" },
            { "chara/monster/m0059/obj/body/b0001/texture/v01_m0059b0001_d.tex", "bolt_d.tex" },
            { "chara/monster/m0059/obj/body/b0001/texture/v01_m0059b0001_id.tex", "bolt_id.tex" },
            { "chara/monster/m0059/obj/body/b0001/texture/v01_m0059b0001_n.tex", "bolt_n.tex" },
            { "chara/monster/m0059/obj/body/b0001/texture/v01_m0059b0001_s.tex", "bolt_s.tex" },
        };

        foreach (var r in replacements)
        {
            var replacementPath = Path.Combine("models", "bolt", r.Value);
            replacementPath = dalamud.PluginInterface.GetResourcePath(replacementPath);
            world.Entity()
                .Set(new FileReplacement(r.Key, replacementPath))
                .ChildOf(bolt);
        }

        return bolt;
    }
}
