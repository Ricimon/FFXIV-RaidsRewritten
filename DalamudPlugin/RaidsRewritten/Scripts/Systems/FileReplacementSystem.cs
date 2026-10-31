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
            .Each((Iter it, int i, ref Model model, ref FileReplacement replace) =>
            {
                var entity = it.Entity(i);

                if (!model.DrawEnabled && replace.FramesSinceApplication < 0)
                {
                    resourceLoader.AddFileReplacement(replace.OriginalPath, replace.ReplacementPath);
                    replace.FramesSinceApplication = 0;
                    return;
                }

                if (replace.FramesSinceApplication >= 0)
                {
                    float durationLeft = 0;
                    if (model.DrawEnabled)
                    {
                        replace.FramesSinceApplication++;
                        if (entity.Has<FileReplacementDuration>())
                        {
                            var duration = entity.Get<FileReplacementDuration>();
                            duration.Seconds -= it.DeltaTime();
                            durationLeft = duration.Seconds;
                        }
                    }

                    // Replacements need to stay for a few frames for model loading systems to pick them up
                    if (replace.FramesSinceApplication >= 30 && durationLeft <= 0)
                    {
                        resourceLoader.RemoveFileReplacement(replace.OriginalPath);
                        replace.FramesSinceApplication = -1;
                    }
                }
            });

        world.System<ActorVfx, FileReplacement>().TermAt(0).Up()
            .Each((Iter it, int i, ref ActorVfx vfx, ref FileReplacement replace) =>
            {
                var entity = it.Entity(i);

                if (vfx.VfxPtr == null && replace.FramesSinceApplication < 0)
                {
                    resourceLoader.AddFileReplacement(replace.OriginalPath, replace.ReplacementPath);
                    replace.FramesSinceApplication = 0;
                    return;
                }

                if (replace.FramesSinceApplication >= 0)
                {
                    float durationLeft = 0;
                    if (vfx.VfxPtr != null)
                    {
                        replace.FramesSinceApplication++;
                        if (entity.Has<FileReplacementDuration>())
                        {
                            var duration = entity.Get<FileReplacementDuration>();
                            duration.Seconds -= it.DeltaTime();
                            durationLeft = duration.Seconds;
                        }
                    }

                    // Replacements need to stay for a few frames for loading systems to pick them up
                    if (replace.FramesSinceApplication >= 30 && durationLeft <= 0)
                    {
                        resourceLoader.RemoveFileReplacement(replace.OriginalPath);
                        replace.FramesSinceApplication = -1;
                    }
                }
            });

        world.System<FlyText, FileReplacement>().Immediate()
            .Each((ref FlyText _, ref FileReplacement replace) =>
            {
                if (replace.FramesSinceApplication < 0)
                {
                    resourceLoader.AddFileReplacement(replace.OriginalPath, replace.ReplacementPath);
                    replace.FramesSinceApplication = 0;
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
