using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class ForcedMarch : ISystem
{
    public const int DebuffId = 0x5CA70;
    public const int MarchId = 0x5CA71;

    public record struct Component(Vector3 MoveDirection);

    /// <summary>
    /// Pending debuff component — no movement yet, just the warning icon.
    /// Stores the direction to march when the debuff expires.
    /// </summary>
    public record struct Pending(Vector3 MoveDirection, float MarchDuration);

    public enum Direction
    {
        Facing,   // 215773 (generic, direction determined by world vector)
        Forward,  // 215774
        Backward, // 215775 (About Face)
        Left,     // 215776
        Right,    // 215777
    }

    private static (int icon, string name, string description) GetDebuffInfo(Direction dir) => dir switch
    {
        Direction.Forward => (215774, "Forward March", "Will be forced to march forward."),
        Direction.Backward => (215775, "About Face", "Will be forced to march backward."),
        Direction.Left => (215776, "Left Face", "Will be forced to march left."),
        Direction.Right => (215777, "Right Face", "Will be forced to march right."),
        _ => (215773, "Forced March", "Advancing inexorably."),
    };

    /// <summary>
    /// Applies a directional debuff that shows a warning icon.
    /// When the debuff expires, the player is forced to march in the given direction.
    /// </summary>
    /// <param name="debuffDuration">How long the warning debuff lasts before march activates.</param>
    /// <param name="marchDuration">How long the forced march movement lasts.</param>
    public static void ApplyToTarget(Entity target, Vector3 direction, float debuffDuration, float marchDuration, Direction dir)
    {
        var (icon, name, description) = GetDebuffInfo(dir);

        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, name, debuffDuration, DebuffId, false, false);

            condition.Set(new Condition.Status(icon, name, description)).Add<Condition.StatusEnfeeblement>();
            condition.Set(new Pending(Vector3.Normalize(direction), marchDuration));
        }, 0, true).ChildOf(target);
    }

    /// <summary>
    /// Directly applies the forced march movement (no warning debuff phase).
    /// </summary>
    public static void ApplyMarchToTarget(Entity target, Vector3 direction, float duration)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, "Forced March", duration, MarchId, false, false);

            condition.Set(new Condition.Status(215773, "Forced March", "Advancing inexorably.")).Add<Condition.StatusEnfeeblement>();
            condition.Set(new Component(Vector3.Normalize(direction)));

            it.World().Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
                .ChildOf(condition);
        }, 0, true).ChildOf(target);
    }

    public void Register(World world)
    {
        // When a pending march debuff is removed (expired), trigger the actual march.
        world.Observer<Pending>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Pending pending) =>
            {
                var parent = e.Parent();
                if (parent.IsValid())
                {
                    ApplyMarchToTarget(parent, pending.MoveDirection, pending.MarchDuration);
                }
            });
    }
}
