using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public class InputSystem : ISystem
{
    private readonly DalamudServices dalamud;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private bool mouseLeftState;
    private bool mouseRightState;

    public InputSystem(DalamudServices dalamud, InputEventSource inputEventSource, Configuration configuration, ILogger logger)
    {
        this.dalamud = dalamud;
        this.configuration = configuration;
        this.logger = logger;

        inputEventSource.SubscribeToKeyDown(args =>
        {
            switch (args.Key)
            {
                case WindowsInput.Events.KeyCode.LButton:
                    mouseLeftState = true;
                    break;
                case WindowsInput.Events.KeyCode.RButton:
                    mouseRightState = true;
                    break;
            }
        });

        inputEventSource.SubscribeToKeyUp(args =>
        {
            switch (args.Key)
            {
                case WindowsInput.Events.KeyCode.LButton:
                    mouseLeftState = false;
                    break;
                case WindowsInput.Events.KeyCode.RButton:
                    mouseRightState = false;
                    break;
            }
        });
    }

    public void Register(World world)
    {
        world
            .Set(new MouseLeftState())
            .Set(new MouseRightState());

        world.System<MouseLeftState>()
            .Each((ref MouseLeftState s) =>
            {
                var wasPressed = s.IsPressed;
                s.IsPressed = mouseLeftState;
                s.IsPressedThisTick = false;
                if (s.IsPressed && !wasPressed) { s.IsPressedThisTick = true; }
            });

        world.System<MouseRightState>()
            .Each((ref MouseRightState s) =>
            {
                var wasPressed = s.IsPressed;
                s.IsPressed = mouseRightState;
                s.IsPressedThisTick = false;
                if (s.IsPressed && !wasPressed) { s.IsPressedThisTick = true; }
            });

        // Game specific stuff
        world.System<PlaceMechanicWithMouse>()
            .Each((Iter it, int i, ref PlaceMechanicWithMouse place) =>
            {
                var entity = it.Entity(i);

                if (configuration.EverythingDisabled)
                {
                    entity.Destruct();
                    return;
                }

                ref readonly var mouseRight = ref it.World().Get<MouseRightState>();
                if (mouseRight.IsPressedThisTick && !IsHoveringUi())
                {
                    entity.Destruct();
                    return;
                }

                // Ground target reticle
                if (!entity.HasChild<PlacementReticle>())
                {
                    it.World().Entity()
                        .Set(new StaticVfx("vfx/common/eff/gl_target1.avfx"))
                        .Set(new Scale(place.ReticleRadius * Vector3.One))
                        .Set(new VfxFadeOutDuration(0.5f))
                        .Add<PlacementReticle>()
                        .ChildOf(entity);
                }

                var mousePosition = ImGui.GetMousePos();
                var localPlayer = dalamud.ObjectTable.LocalPlayer;
                if (localPlayer != null &&
                    !IsHoveringUi() &&
                    dalamud.GameGui.ScreenToWorld(mousePosition, out var worldPos))
                {
                    worldPos.Y = localPlayer.Position.Y;
                    var playerToWorldPos = worldPos - localPlayer.Position;
                    var rotation = MathUtilities.VectorToRotation(playerToWorldPos.ToVector2());
                    entity.Children(c =>
                    {
                        if (c.Has<PlacementReticle>())
                        {
                            c.Set(new Position(worldPos)).Set(new Rotation(rotation));
                        }
                    });

                    ref readonly var mouseLeft = ref it.World().Get<MouseLeftState>();
                    if (mouseLeft.IsPressedThisTick)
                    {
                        entity.Children(c =>
                        {
                            // Used for testing arbitrarily placing static vfx
                            if (!c.Has<PlacementReticle>() && c.TryGet<StaticVfx>(out var sv))
                            {
                                it.World().Entity()
                                    .Set(sv)
                                    .Set(new Position(worldPos))
                                    .Set(new Rotation(rotation))
                                    .Set(new Scale())
                                    .Add<Attack>()
                                    .Add<Omen>();
                            }
                        });
                        entity.Destruct();
                    }
                }
                else
                {
                    entity.Children(c =>
                    {
                        if (c.Has<PlacementReticle>())
                        {
                            c.Destruct();
                        }
                    });
                }
            });

        // Ensure at max only one PlaceMechanicWithMouse exists
        world.Observer<PlaceMechanicWithMouse>()
            .Event(Ecs.OnAdd)
            .Each((Iter it, int i, ref PlaceMechanicWithMouse _) =>
            {
                var world = it.World();
                var entity = it.Entity(i);

                var isDeferrred = world.IsDeferred();
                if (!isDeferrred) { world.DeferBegin(); }
                it.World().Query<PlaceMechanicWithMouse>().Each((Entity e, ref PlaceMechanicWithMouse _) =>
                {
                    if (entity.Id != e.Id)
                    {
                        e.Destruct();
                    }
                });
                if (!isDeferrred) { world.DeferEnd(); }
            });
    }

    private bool IsHoveringUi()
    {
        return IsHoveringGameUi() || ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow);
    }

    private unsafe bool IsHoveringGameUi()
    {
        var collisionNode = AtkStage.Instance()->AtkCollisionManager->IntersectingCollisionNode;
        var addon = AtkStage.Instance()->AtkCollisionManager->IntersectingAddon;

        if (collisionNode == null && addon == null) { return false; }
        // World UI such as Nameplates have this flag
        if (collisionNode != null && collisionNode->NodeFlags.HasFlag(NodeFlags.UseDepthBasedPriority)) { return false; }

        return true;
    }
}
