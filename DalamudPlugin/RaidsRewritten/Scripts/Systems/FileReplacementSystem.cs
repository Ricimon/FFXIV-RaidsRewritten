using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Systems;

public class FileReplacementSystem(ResourceLoader resourceLoader) : ISystem
{
    public void Register(World world)
    {
        world.System<Model, FileReplacement>().TermAt(0).Up()
            .Each((ref Model model, ref FileReplacement replace) =>
            {
                if (!model.DrawEnabled && replace.FramesSinceApplication < 0)
                {
                    resourceLoader.AddFileReplacement(replace.OriginalPath, replace.ReplacementPath);
                    replace.FramesSinceApplication = 0;
                    return;
                }

                if (replace.FramesSinceApplication >= 0)
                {
                    if (model.DrawEnabled)
                    {
                        replace.FramesSinceApplication++;
                    }

                    // Replacements need to stay for a few frames for model loading systems to pick them up
                    if (replace.FramesSinceApplication == 5)
                    {
                        resourceLoader.RemoveFileReplacement(replace.OriginalPath);
                        replace.FramesSinceApplication = -1;
                    }
                }
            });

        world.Observer<FileReplacement>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref FileReplacement _) =>
            {
                var replace = e.Get<FileReplacement>();
                if (replace.FramesSinceApplication >= 0)
                {
                    resourceLoader.RemoveFileReplacement(replace.OriginalPath);
                }
            });
    }
}
