using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

/// <summary>
/// Acceleration Bomb (status 1072): Shows a warning debuff.
/// If the player is moving at the moment the debuff expires, they are punished with a Stun.
/// </summary>
public class AccelerationBomb(DalamudServices dalamud) : ISystem
{
    public const int Id = 0x430; // 1072

    public record struct Component(Vector3 LastPosition, bool IsMoving = false);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();
            var initialPos = target.Get<Player.Component>().PlayerCharacter?.Position ?? Vector3.Zero;

            var condition = Condition.ApplyToTarget(target, "Acceleration Bomb", duration, Id, extendDuration, overrideExistingDuration);

            condition.Set(new Condition.Status(215727, "Acceleration Bomb",
                "An acceleration-trigger explosive is affixed to the body. Any movement when effect wears off will result in detonation."))
                .Add<Condition.StatusEnfeeblement>();

            if (!condition.Has<Component>())
            {
                condition.Set(new Component(initialPos));

                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"))
                    .ChildOf(condition);
            }
        }, 0, true).ChildOf(target);
    }

    public void Register(World world)
    {
        // Track whether the player is moving each frame while the bomb is ticking.
        world.System<Component>()
            .With<Player.LocalPlayer>().Up()
            .Each((Entity e, ref Component bomb) =>
            {
                var player = dalamud.ObjectTable.LocalPlayer;
                if (player == null) return;

                var currentPos = player.Position;
                bomb.IsMoving = Vector2.Distance(currentPos.ToVector2(), bomb.LastPosition.ToVector2()) > 0.05f;
                bomb.LastPosition = currentPos;
            });

        // When the condition expires, check if the player was moving — if so, detonate.
        world.Observer<Component>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Component bomb) =>
            {
                if (!bomb.IsMoving) return;

                var parent = e.Parent();
                if (!parent.IsValid()) return;

                Stun.ApplyToTarget(parent, 3.0f);
            });
    }
}
