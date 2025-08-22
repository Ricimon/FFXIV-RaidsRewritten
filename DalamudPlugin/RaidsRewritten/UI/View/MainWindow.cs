using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Audio;
using RaidsRewritten.Data;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.UI.Util;
using RaidsRewritten.Utility;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace RaidsRewritten.UI.View;

public class MainWindow : Window, IPluginUIView, IDisposable
{
    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    public IReactiveProperty<bool> PublicRoom { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<string> RoomName { get; } = new ReactiveProperty<string>(string.Empty);
    public IReactiveProperty<string> RoomPassword { get; } = new ReactiveProperty<string>(string.Empty);

    private readonly Subject<Unit> joinRoom = new();
    public IObservable<Unit> JoinRoom => joinRoom.AsObservable();
    private readonly Subject<Unit> leaveRoom = new();
    public IObservable<Unit> LeaveRoom => leaveRoom.AsObservable();

    public IReactiveProperty<Keybind> KeybindBeingEdited { get; } = new ReactiveProperty<Keybind>();
    public IObservable<Keybind> ClearKeybind => clearKeybind.AsObservable();
    private readonly Subject<Keybind> clearKeybind = new();

    public IReactiveProperty<bool> EnableGroundPings { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> EnablePingWheel { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> EnableGuiPings { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> EnableHpMpPings { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> SendGuiPingsToCustomServer { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> SendGuiPingsToXivChat { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<XivChatSendLocation> XivChatSendLocation { get; } = new ReactiveProperty<XivChatSendLocation>();

    public IObservable<Unit> PrintNodeMap1 => printNodeMap1.AsObservable();
    private readonly Subject<Unit> printNodeMap1 = new();
    public IObservable<Unit> PrintNodeMap2 => printNodeMap2.AsObservable();
    private readonly Subject<Unit> printNodeMap2 = new();
    public IObservable<Unit> PrintPartyStatuses => printPartyStatuses.AsObservable();
    private readonly Subject<Unit> printPartyStatuses = new();
    public IObservable<Unit> PrintTargetStatuses => printTargetStatuses.AsObservable();
    private readonly Subject<Unit> printTargetStatuses = new();

    public IReactiveProperty<float> MasterVolume { get; } = new ReactiveProperty<float>();

    public IReactiveProperty<bool> PlayRoomJoinAndLeaveSounds { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> KeybindsRequireGameFocus { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<bool> PrintLogsToChat { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<int> MinimumVisibleLogLevel { get; } = new ReactiveProperty<int>();

    private readonly WindowSystem windowSystem;
    private readonly DalamudServices dalamud;
    private readonly ServerConnection serverConnection;
    private readonly MapManager mapChangeHandler;
    private readonly EncounterManager encounterManager;
    private readonly AttackManager attackManager;
    private readonly Mechanic.Factory mechanicFactory;
    private readonly Configuration configuration;
    private readonly EcsContainer ecsContainer;
    private readonly VfxSpawn vfxSpawn;
    private readonly ILogger logger;

    private readonly string windowName;
    private readonly string[] xivChatSendLocations;
    private readonly string[] falloffTypes;
    private readonly string[] allLoggingLevels;

    private string? createPrivateRoomButtonText;

    private string[]? inputDevices;
    private string[]? outputDevices;

    private int effectsRendererPositionX = 0;
    private int effectsRendererPositionY = 0;

    public MainWindow(
        WindowSystem windowSystem,
        DalamudServices dalamud,
        ServerConnection serverConnection,
        MapManager mapChangeHandler,
        EncounterManager encounterManager,
        AttackManager attackManager,
        Mechanic.Factory mechanicFactory,
        Configuration configuration,
        EcsContainer ecsContainer,
        VfxSpawn vfxSpawn,
        ILogger logger) : base(
        PluginInitializer.Name)
    {
        this.windowSystem = windowSystem;
        this.dalamud = dalamud;
        this.serverConnection = serverConnection;
        this.mapChangeHandler = mapChangeHandler;
        this.encounterManager = encounterManager;
        this.attackManager = attackManager;
        this.mechanicFactory = mechanicFactory;
        this.configuration = configuration;
        this.ecsContainer = ecsContainer;
        this.vfxSpawn = vfxSpawn;
        this.logger = logger;

        var version = GetType().Assembly.GetName().Version?.ToString() ?? string.Empty;
        this.windowName = PluginInitializer.Name;
        if (version.Length > 0)
        {
            var versionArray = version.Split(".");
            version = string.Join(".", versionArray.Take(3));
            this.windowName += $" v{version}";
        }
#if DEBUG
        this.windowName += " (DEBUG)";
#endif
        this.xivChatSendLocations = Enum.GetNames<XivChatSendLocation>();
        this.falloffTypes = Enum.GetNames<AudioFalloffModel.FalloffType>();
        this.allLoggingLevels = [.. LogLevel.AllLoggingLevels.Select(l => l.Name)];
        windowSystem.AddWindow(this);

        this.effectsRendererPositionX = configuration.EffectsRendererPositionX;
        this.effectsRendererPositionY = configuration.EffectsRendererPositionY;


        if(effectsRendererPositionX == 0 && effectsRendererPositionY == 0) { 
            var viewport = ImGui.GetMainViewport();
            int x = (int)(viewport.Pos.X + viewport.Size.X / 2);
            int y = (int)(viewport.Pos.Y + viewport.Size.Y / 3);


            effectsRendererPositionX = x;
            effectsRendererPositionY = y;

            configuration.EffectsRendererPositionX = effectsRendererPositionX;
            configuration.EffectsRendererPositionY = effectsRendererPositionY;
            configuration.Save();
        }


#if DEBUG
        visible = true;
#endif
    }

    public override void Draw()
    {
        if (!Visible)
        {
            this.createPrivateRoomButtonText = null;
            return;
        }

        var width = 350;
        ImGui.SetNextWindowSize(new Vector2(width, 400), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 250), new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin(this.windowName, ref this.visible))
        {
            this.ecsContainer.World.DeferBegin();
            DrawContents();
            this.ecsContainer.World.DeferEnd();
        }
        ImGui.End();
    }

    public void Dispose()
    {
        windowSystem.RemoveWindow(this);
        GC.SuppressFinalize(this);
    }

    private void DrawContents()
    {
        if (!this.configuration.EverythingDisabled)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4Colors.DarkRed.ToColorU32()))
            {
                if (ImGui.Button("SHUTOFF EVERYTHING",
                    new Vector2(ImGui.GetWindowWidth() - 2 * ImGui.GetStyle().WindowPadding.X, 0)))
                {
                    this.configuration.EverythingDisabled = true;
                    this.configuration.Save();
                    // Delete everything
                    this.attackManager.ClearAllAttacks();
                    this.ecsContainer.World.DeleteWith<Condition.Component>();
                    this.ecsContainer.World.DeleteWith<DelayedAction.Component>();
                    this.vfxSpawn.Clear();
                }
            }
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4Colors.DarkGreen.ToColorU32()))
            {
                if (ImGui.Button("Enable Plugin",
                    new Vector2(ImGui.GetWindowWidth() - 2 * ImGui.GetStyle().WindowPadding.X, 0)))
                {
                    this.configuration.EverythingDisabled = false;
                    this.configuration.Save();
                    var mechanics = this.encounterManager.ActiveEncounter?.GetMechanics();
                    if (mechanics != null)
                    {
                        foreach(var mechanic in mechanics)
                        {
                            mechanic.Reset();
                        }
                    }
                }
            }
        }

        using var tabs = ImRaii.TabBar("sp-tabs");
        if (!tabs) return;

        DrawMainTab();
        DrawDebugTab();
        //DrawPublicTab();
        //DrawPrivateTab();
        //DrawConfigTab();
        //DrawMiscTab();
    }

    private void DrawMainTab()
    {
        using var mainTab = ImRaii.TabItem("Main");
        if (!mainTab) return;

        if (ImGui.InputInt("Position X", ref effectsRendererPositionX))
        {
            configuration.EffectsRendererPositionX = effectsRendererPositionX;
            configuration.Save();
        }

        if (ImGui.InputInt("Position Y", ref effectsRendererPositionY))
        {
            configuration.EffectsRendererPositionY = effectsRendererPositionY;
            configuration.Save();
        }

        if (ImGui.Button("Reset Status Placement"))
        {
            var viewport = ImGui.GetMainViewport();
            int x = (int)(viewport.Pos.X + viewport.Size.X / 2);
            int y = (int)(viewport.Pos.Y + viewport.Size.Y / 3);

            effectsRendererPositionX = x;
            effectsRendererPositionY = y;

            configuration.EffectsRendererPositionX = effectsRendererPositionX;
            configuration.EffectsRendererPositionY = effectsRendererPositionY;
            configuration.Save();
        }

        ImGui.SameLine();
        
        if (ImGui.Button("Status Overlay"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                ecsContainer.World.Entity().Set(new Condition.Component("test", 15.0f)).ChildOf(e);
            });
        }

        var encounterText = new StringBuilder("Active Encounter: ");
        if (encounterManager.ActiveEncounter == null)
        {
            encounterText.Append("None");
            ImGui.Text(encounterText.ToString());
        }
        else
        {
            encounterText.Append(encounterManager.ActiveEncounter.Name);
            ImGui.Text(encounterText.ToString());

            using (ImRaii.PushIndent())
            {
                encounterManager.ActiveEncounter.DrawConfig();
            }
        }
    }

    private void DrawDebugTab()
    {
        using var debugTab = ImRaii.TabItem("Debug");
        if (!debugTab) return;

        if (ImGui.Button("Clear All Attacks"))
        {
            this.attackManager.ClearAllAttacks();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear All Statuses"))
        {
            this.ecsContainer.World.DeleteWith<Condition.Component>();
        }

        if (ImGui.Button("Circle Omen"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<CircleOmen>(out var circle))
                {
                    circle.Set(new Position(player.Position));
                    circle.Set(new Rotation(player.Rotation));
                    circle.Set(new Scale(Vector3.One));
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Fan Omen"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<FanOmen>(out var fan))
                {
                    fan.Set(new Position(player.Position));
                    fan.Set(new Rotation(player.Rotation));
                    fan.Set(new Scale(Vector3.One));
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Rect Omen"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<RectangleOmen>(out var rect))
                {
                    rect.Set(new Position(player.Position));
                    rect.Set(new Rotation(player.Rotation));
                    rect.Set(new Scale(Vector3.One));
                }
            }
        }

        if (ImGui.Button("Print Player Data"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                this.logger.Info($"Player position:{player.Position}, address:0x{player.Address:X}, entityId:0x{player.EntityId:X}, gameObjectId:0x{player.GameObjectId}");
            }
        }

#if DEBUG
        bool punishmentImmunity = configuration.PunishmentImmunity;
        if (ImGui.Checkbox("Punishment Immunity", ref punishmentImmunity))
        {
            configuration.PunishmentImmunity = punishmentImmunity;
            configuration.Save();
        }
#endif

        ImGui.Text("Fake statuses");
        if (ImGui.Button("Bind"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Bind.ApplyToPlayer(e, 2.0f);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Knockback"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Knockback.ApplyToPlayer(e, new Vector3(1, 0, 0), 2.0f, true);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Stun"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Stun.ApplyToPlayer(e, 2.0f);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Paralysis"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Paralysis.ApplyToPlayer(e, 5.0f, 3.0f, 1.0f, -100);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Heavy"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Heavy.ApplyToPlayer(e, 5.0f);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Heavy (e)"))
        {
            using var q = ecsContainer.World.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Heavy.ApplyToPlayer(e, 5.0f, -100, true);
            });
        }

        ImGui.Text("Attacks");

        if (ImGui.Button("Spawn Twister"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<Twister>(out var twister))
                {
                    twister.Set(new Position(player.Position));
                    twister.Set(new Rotation(player.Rotation));
                }
            }
        }

#if DEBUG
        if (ImGui.Button("Spawn Ball"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<RollingBall>(out var ball))
                {
                    ball.Set(new Position(player.Position))
                        .Set(new Rotation(player.Rotation))
                        .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(player.Rotation)))
                        .Set(new RollingBall.CircleArena(player.Position.ToVector2(), 10.0f));
                        //.Set(new RollingBall.ShowOmen());
                }
            }
        }
        if (ImGui.Button("LightningCorridor"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<LightningCorridor>(out var attack))
                {
                    attack.Set(new Position(player.Position))
                        .Set(new Rotation(player.Rotation));
                }
            }
        }

        if (ImGui.Button("Exaflare"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<Exaflare>(out var exaflare))
                {
                    exaflare.Set(new Position(player.Position))
                        .Set(new Rotation(player.Rotation));
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Row of Exaflares"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<ExaflareRow>(out var exaflare))
                {
                    exaflare.Set(new Position(player.Position))
                        .Set(new Rotation(player.Rotation));
                }
            }
        }

        if (ImGui.Button("Jumpwave"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<JumpableShockwave>(out var jumpwave))
                {
                    jumpwave.Set(new Position(player.Position + 0.0f * Vector3.UnitX))
                        .Set(new Rotation(player.Rotation));
                }
            }
        }

        if (ImGui.Button("Dreadknight"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
                {
                    dreadknight.Set(new Position(player.Position));
                }
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Dreadknight With Tether"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
                {
                    dreadknight.Set(new Position(player.Position));
                    Dreadknight.ApplyTarget(dreadknight, player);
                }
            }
        }

        if (ImGui.Button("ADS"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<ADS>(out var ads))
                {
                    var originalPosition = player.Position;
                    ads.Set(new Position(player.Position))
                        .Set(new Rotation(player.Rotation));
                    DelayedAction.Create(ads.CsWorld(), () =>
                    {
                        var player = this.dalamud.ClientState.LocalPlayer;
                        if (player != null)
                        {
                            ADS.CastLineAoe(ads, MathUtilities.GetAbsoluteAngleFromSourceToTarget(originalPosition, player.Position));
                        }
                    }, 3f);
                    DelayedAction.Create(ads.CsWorld(), () =>
                    {
                        var player = this.dalamud.ClientState.LocalPlayer;
                        if (player != null)
                        {
                            ADS.CastLineAoe(ads, MathUtilities.GetAbsoluteAngleFromSourceToTarget(originalPosition, player.Position));
                        }
                    }, 9f);
                    DelayedAction.Create(ads.CsWorld(), ads.Destruct, 15f);
                }
            }
        }

        if (ImGui.Button("Close Tether to Target"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                var target = player.TargetObject;
                if (target != null)
                {
                    if (this.attackManager.TryCreateAttackEntity<DistanceTether>(out var tether))
                    {
                        DistanceTether.SetTetherVfx(
                            tether,
                            DistanceTether.TetherVfxes[DistanceTether.TetherVfx.DelayedClose]
                            )
                            .Set(new ActorVfxSource(player))
                            .Set(new ActorVfxTarget(target))
                            .Set(new DistanceTether.VfxOnCondition(["vfx/monster/m0005/eff/m0005sp_15t0t.avfx"]))
                            .Set(new DistanceTether.Tether(
                                    (distance) => distance > 10,
                                    (e) => { Stun.ApplyToPlayer(e, 5); },
                                    () => { DistanceTether.RemoveTetherVfx(tether); }
                                ));

                        DelayedAction.Create(tether.CsWorld(), () =>
                        {
                            DistanceTether.SetTetherVfx(
                                tether,
                                DistanceTether.TetherVfxes[DistanceTether.TetherVfx.ActivatedClose]
                                )
                                .Add<DistanceTether.Activated>();
                        }, 1f);

                        DelayedAction.Create(tether.CsWorld(), () =>
                        {
                            tether.Destruct();
                        }, 5f);
                    }
                }
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Far Tether to Target"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                var target = player.TargetObject;
                if (target != null)
                {
                    if (this.attackManager.TryCreateAttackEntity<DistanceTether>(out var tether))
                    {
                        DistanceTether.SetTetherVfx(
                            tether,
                            DistanceTether.TetherVfxes[DistanceTether.TetherVfx.DelayedFar]
                            )
                            .Set(new ActorVfxSource(player))
                            .Set(new ActorVfxTarget(target))
                            .Set(new DistanceTether.VfxOnCondition(["vfx/monster/m0005/eff/m0005sp_15t0t.avfx"]))
                            .Set(new DistanceTether.Tether(
                                    (distance) => distance < 10,
                                    (e) => { Stun.ApplyToPlayer(e, 5); },
                                    () => { DistanceTether.RemoveTetherVfx(tether); }
                                ));

                        DelayedAction.Create(tether.CsWorld(), () =>
                        {
                            DistanceTether.SetTetherVfx(
                                tether,
                                DistanceTether.TetherVfxes[DistanceTether.TetherVfx.ActivatedFar]
                                )
                                .Add<DistanceTether.Activated>();
                        }, 1f);

                        DelayedAction.Create(tether.CsWorld(), () =>
                        {
                            tether.Destruct();
                        }, 5f);
                    }
                }
            }
        }

        ImGui.Text("Heat Stuff");
        if (ImGui.Button("Add Temperature"))
        {
            var world = ecsContainer.World;
            using var q = world.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Temperature.SetTemperature(e);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Incr Heat"))
        {
            var world = ecsContainer.World;
            using var q = world.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Temperature.HeatChangedEvent(e, 50);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Decr Heat"))
        {
            var world = ecsContainer.World;
            using var q = world.Query<Player.Component>();
            q.Each((Entity e, ref Player.Component pc) =>
            {
                Temperature.HeatChangedEvent(e, -50);
            });
        }
	    if (ImGui.Button("Spawn Liquid Heaven"))
        {
            var player = this.dalamud.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<LiquidHeaven>(out var LiquidHeaven))
                {
                    LiquidHeaven.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                }
            }
        }
#endif
    }

    #region Rooms
    private void DrawPublicTab()
    {
        using var publicTab = ImRaii.TabItem("Public room");
        if (!publicTab) return;

        this.PublicRoom.Value = true;

        ImGui.BeginDisabled(!this.serverConnection.ShouldBeInRoom);
        ImGui.Text(string.Format("Room ID: {0}", this.mapChangeHandler.GetCurrentMapPublicRoomName()));
        ImGui.EndDisabled();

#if DEBUG
        unsafe
        {
            ImGui.Text(string.Format("(DEBUG) Territory type: {0}", ((TerritoryIntendedUseEnum)FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()->CurrentTerritoryIntendedUseId).ToString()));
        }
#endif

        ImGui.BeginDisabled(this.serverConnection.ShouldBeInRoom);
        if (ImGui.Button("Join Public Room"))
        {
            this.joinRoom.OnNext(Unit.Default);
        }
        ImGui.EndDisabled();

        var dcMsg = this.serverConnection.Channel?.LatestServerDisconnectMessage;
        if (dcMsg != null)
        {
            ImGui.SameLine();
            using var c = ImRaii.PushColor(ImGuiCol.Text, Vector4Colors.Red);
            ImGui.Text("Unknown error (see /xllog)");
        }

        ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------

        if (this.serverConnection.InRoom)
        {
            DrawServerRoom();
            ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------
        }
    }

    private void DrawPrivateTab()
    {
        using var privateTab = ImRaii.TabItem("Private room");
        if (!privateTab) return;

        this.PublicRoom.Value = false;

        ImGuiInputTextFlags readOnlyIfInRoom = this.serverConnection.InRoom ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        string roomName = this.RoomName.Value;

        if (ImGui.InputText("Room Name", ref roomName, 100, ImGuiInputTextFlags.AutoSelectAll | readOnlyIfInRoom))
        {
            this.RoomName.Value = roomName;
        }
        ImGui.SameLine(); Common.HelpMarker("Leave blank to join your own room");

        string roomPassword = this.RoomPassword.Value;
        ImGui.PushItemWidth(38);
        if (ImGui.InputText("Room Password (up to 4 digits)", ref roomPassword, 4, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll | readOnlyIfInRoom))
        {
            this.RoomPassword.Value = roomPassword;
        }
        ImGui.PopItemWidth();
        if (!ImGui.IsItemActive())
        {
            while (roomPassword.Length < 4)
            {
                roomPassword = "0" + roomPassword;
            }
            this.RoomPassword.Value = roomPassword;
        }
        ImGui.SameLine(); Common.HelpMarker("Sets the password if joining your own room");

        ImGui.BeginDisabled(this.serverConnection.InRoom);
        if (this.createPrivateRoomButtonText == null || !this.serverConnection.InRoom)
        {
            var playerName = this.dalamud.ClientState.GetLocalPlayerFullName();
            this.createPrivateRoomButtonText = roomName.Length == 0 || roomName == playerName ?
                "Create Private Room" : "Join Private Room";
        }
        if (ImGui.Button(this.createPrivateRoomButtonText))
        {
            this.joinRoom.OnNext(Unit.Default);
        }
        ImGui.EndDisabled();

        var dcMsg = this.serverConnection.Channel?.LatestServerDisconnectMessage;
        if (dcMsg != null)
        {
            ImGui.SameLine();
            using var c = ImRaii.PushColor(ImGuiCol.Text, Vector4Colors.Red);
            // this is kinda scuffed but will do for now
            if (dcMsg.Contains("incorrect password"))
            {
                ImGui.Text("Incorrect password");
            }
            else if (dcMsg.Contains("room does not exist"))
            {
                ImGui.Text("Room not found");
            }
            else
            {
                ImGui.Text("Unknown error (see /xllog)");
            }
        }

        ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------

        if (this.serverConnection.InRoom)
        {
            DrawServerRoom();
            ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------
        }
    }

    private void DrawServerRoom()
    {
        ImGui.AlignTextToFramePadding();
        var roomName = this.serverConnection.Channel?.RoomName;
        if (string.IsNullOrEmpty(roomName) || roomName.StartsWith("public"))
        {
            ImGui.Text("Public Room");
        }
        else
        {
            ImGui.Text($"{roomName}'s Room");
        }
        if (this.serverConnection.ShouldBeInRoom)
        {
            ImGui.SameLine();
            if (ImGui.Button("Leave"))
            {
                this.leaveRoom.OnNext(Unit.Default);
            }
        }

        var indent = 10;
        ImGui.Indent(indent);

        foreach (var (playerName, index) in this.serverConnection.PlayersInRoom.Select((p, i) => (p, i)))
        {
            Vector4 color = Vector4Colors.Red;
            string tooltip = "Connection Error";
            bool connected = false;

            // Assume first player is always the local player
            if (index == 0)
            {
                var channel = this.serverConnection.Channel;
                if (channel != null)
                {
                    if (channel.Connected)
                    {
                        color = Vector4Colors.Green;
                        tooltip = "Connected";
                        connected = true;
                    }
                    else if (channel.Connecting)
                    {
                        color = Vector4Colors.Orange;
                        tooltip = "Connecting";
                    }
                }
            }
            else
            {
                // Other players are always connected
                color = Vector4Colors.Green;
                tooltip = "Connected";
                connected = true;
            }

            // Highlight row on hover
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var h = ImGui.GetTextLineHeightWithSpacing();
            var rowMin = new Vector2(ImGui.GetWindowPos().X, pos.Y);
            var rowMax = new Vector2(rowMin.X + ImGui.GetWindowWidth(), pos.Y + h);
            if (ImGui.IsMouseHoveringRect(rowMin, rowMax))
            {
                drawList.AddRectFilled(rowMin, rowMax, ImGui.ColorConvertFloat4ToU32(Vector4Colors.Gray));
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup($"peer-menu-{index}");
                }
            }
            using (var popup = ImRaii.Popup($"peer-menu-{index}"))
            {
                if (popup)
                {
                    ImGui.Text(playerName);
                }
            }

            // Connectivity/activity indicator
            var radius = 0.3f * h;
            pos += new Vector2(0, h / 2f);
            if (index == 0)
            {
                if (connected)
                {
                    drawList.AddCircleFilled(pos, radius, ImGui.ColorConvertFloat4ToU32(color));
                }
                else
                {
                    drawList.AddCircle(pos, radius, ImGui.ColorConvertFloat4ToU32(color));
                }
            }
            else
            {
                if (connected)
                {
                    drawList.AddCircleFilled(pos, radius, ImGui.ColorConvertFloat4ToU32(color));
                }
                else
                {
                    drawList.AddCircle(pos, radius, ImGui.ColorConvertFloat4ToU32(color));
                }
            }
            // Tooltip
            if (Vector2.Distance(ImGui.GetMousePos(), pos) < radius)
            {
                ImGui.SetTooltip(tooltip);
            }
            pos += new Vector2(radius + 3, -h / 2.25f);
            ImGui.SetCursorScreenPos(pos);

            // Player Label
            var playerLabel = new StringBuilder(playerName);
            ImGui.Text(playerLabel.ToString());
        }

        ImGui.Indent(-indent);
    }
    #endregion

    #region Config
    private void DrawConfigTab()
    {
        using var deviceTab = ImRaii.TabItem("Config");
        if (!deviceTab) return;

        //using (var deviceTable = ImRaii.Table("AudioDevices", 2))
        //{
        //    if (deviceTable)
        //    {
        //        ImGui.TableSetupColumn("AudioDevicesCol1", ImGuiTableColumnFlags.WidthFixed, 80);
        //        ImGui.TableSetupColumn("AudioDevicesCol2", ImGuiTableColumnFlags.WidthFixed, 230);

        //        ImGui.TableNextRow(); ImGui.TableNextColumn();
        //        ImGui.AlignTextToFramePadding();
        //        ImGui.Text("Output Device"); ImGui.TableNextColumn();
        //    }
        //}

        ImGui.Text("Keybinds");
        ImGui.SameLine(); Common.HelpMarker("Right click to clear a keybind.");
        using (ImRaii.PushIndent())
        {
        }

        ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------

        var enableGroundPings = this.EnableGroundPings.Value;
        if (ImGui.Checkbox("Enable Ground Pings", ref enableGroundPings))
        {
            this.EnableGroundPings.Value = enableGroundPings;
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!this.EnableGroundPings.Value))
        {
            var enablePingWheel = this.EnablePingWheel.Value;
            if (ImGui.Checkbox("Enable Ping Wheel", ref enablePingWheel))
            {
                this.EnablePingWheel.Value = enablePingWheel;
            }
            ImGui.SameLine(); Common.HelpMarker("More ping types coming soon™");
        }

        ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------

        var enableGuiPings = this.EnableGuiPings.Value;
        if (ImGui.Checkbox("Enable UI Pings", ref enableGuiPings))
        {
            this.EnableGuiPings.Value = enableGuiPings;
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!this.EnableGuiPings.Value))
        {
            var enableHpMpPings = this.EnableHpMpPings.Value;
            if (ImGui.Checkbox("Enable HP/MP Pings", ref enableHpMpPings))
            {
                this.EnableHpMpPings.Value = enableHpMpPings;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Mouse input will be blocked if pinging HP/MP values, so disable this if this is not desired.");
            }
            ImGui.SameLine(); Common.HelpMarker("Only works on party list");

            var sendGuiPingsToCustomServer = this.SendGuiPingsToCustomServer.Value;
            if (ImGui.Checkbox("Send UI pings to joined room", ref sendGuiPingsToCustomServer))
            {
                this.SendGuiPingsToCustomServer.Value = sendGuiPingsToCustomServer;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sends UI pings as /echo messages to other players in the same plugin room. This avoids sending traceable data to XIV servers.");
            }

            var sendGuiPingsToXivChat = this.SendGuiPingsToXivChat.Value;
            if (ImGui.Checkbox("Send UI pings in game chat (!)", ref sendGuiPingsToXivChat))
            {
                this.SendGuiPingsToXivChat.Value = sendGuiPingsToXivChat;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sending messages in game chat may be traceable as plugin usage. Use with caution!");
            }

            using (ImRaii.PushIndent())
            using (ImRaii.Disabled(!this.SendGuiPingsToXivChat.Value))
            using (ImRaii.ItemWidth(100))
            {
                var xivChatSendLocation = (int)this.XivChatSendLocation.Value;
                if (ImGui.Combo("Send Chat To", ref xivChatSendLocation, this.xivChatSendLocations, this.xivChatSendLocations.Length))
                {
                    this.XivChatSendLocation.Value = (XivChatSendLocation)xivChatSendLocation;
                }
            }
        }

#if DEBUG
        ImGui.Dummy(new Vector2(0.0f, 5.0f)); // ---------------

        ImGui.Text("DEBUG");
        if (ImGui.Button("Print Node Map 1"))
        {
            this.printNodeMap1.OnNext(Unit.Default);
        }
        ImGui.SameLine();
        if (ImGui.Button("Print Node Map 2"))
        {
            this.printNodeMap2.OnNext(Unit.Default);
        }

        if (ImGui.Button("Print Party Statuses"))
        {
            this.printPartyStatuses.OnNext(Unit.Default);
        }
        ImGui.SameLine();
        if (ImGui.Button("Print Target Statuses"))
        {
            this.printTargetStatuses.OnNext(Unit.Default);
        }
#endif
    }

    private void DrawMiscTab()
    {
        using var miscTab = ImRaii.TabItem("Misc");
        if (!miscTab) return;

        //var playRoomJoinAndLeaveSounds = this.PlayRoomJoinAndLeaveSounds.Value;
        //if (ImGui.Checkbox("Play room join and leave sounds", ref playRoomJoinAndLeaveSounds))
        //{
        //    this.PlayRoomJoinAndLeaveSounds.Value = playRoomJoinAndLeaveSounds;
        //}

        //var keybindsRequireGameFocus = this.KeybindsRequireGameFocus.Value;
        //if (ImGui.Checkbox("Keybinds require game focus", ref keybindsRequireGameFocus))
        //{
        //    this.KeybindsRequireGameFocus.Value = keybindsRequireGameFocus;
        //}

        var printLogsToChat = this.PrintLogsToChat.Value;
        if (ImGui.Checkbox("Print logs to chat", ref printLogsToChat))
        {
            this.PrintLogsToChat.Value = printLogsToChat;
        }

        if (printLogsToChat)
        {
            ImGui.SameLine();
            var minLogLevel = this.MinimumVisibleLogLevel.Value;
            ImGui.SetNextItemWidth(70);
            if (ImGui.Combo("Min log level", ref minLogLevel, allLoggingLevels, allLoggingLevels.Length))
            {
                this.MinimumVisibleLogLevel.Value = minLogLevel;
            }
        }

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Bugs or suggestions?");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.35f, 0.40f, 0.95f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.41f, 0.45f, 1.0f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.32f, 0.36f, 0.88f, 1));
        if (ImGui.Button("Discord"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://discord.gg/rSucAJ6A7u", UseShellExecute = true });
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.39f, 0.20f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.49f, 0.30f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.92f, 0.36f, 0.18f, 1));
        if (ImGui.Button("Support on Ko-fi"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/ricimon", UseShellExecute = true });
        }
        ImGui.PopStyleColor(3);
    }

    private void DrawKeybindEdit(Keybind keybind, VirtualKey currentBinding, string label, string? tooltip = null)
    {
        using var id = ImRaii.PushId($"{keybind} Keybind");
        {
            if (ImGui.Button(this.KeybindBeingEdited.Value == keybind ?
                    "Recording..." :
                    KeyCodeStrings.TranslateKeyCode(currentBinding),
                new Vector2(5 * ImGui.GetFontSize(), 0)))
            {
                this.KeybindBeingEdited.Value = this.KeybindBeingEdited.Value != keybind ?
                    keybind : Keybind.None;
            }
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            this.clearKeybind.OnNext(keybind);
            this.KeybindBeingEdited.Value = Keybind.None;
        }
        ImGui.SameLine();
        ImGui.Text(label);
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
    }
    #endregion
}
