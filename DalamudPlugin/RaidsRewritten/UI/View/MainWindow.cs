using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using RaidsRewritten.Audio;
using RaidsRewritten.Data;
using RaidsRewritten.Extensions;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.UI.Util;
using Reactive.Bindings;

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
    private readonly DalamudServices dalamudServices;
    private readonly ServerConnection serverConnection;
    private readonly MapManager mapChangeHandler;
    private readonly AttackManager attackManager;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private readonly string[] xivChatSendLocations;
    private readonly string[] falloffTypes;
    private readonly string[] allLoggingLevels;

    private string? createPrivateRoomButtonText;

    private string[]? inputDevices;
    private string[]? outputDevices;

    public MainWindow(
        WindowSystem windowSystem,
        DalamudServices dalamudServices,
        ServerConnection serverConnection,
        MapManager mapChangeHandler,
        AttackManager attackManager,
        Configuration configuration,
        ILogger logger) : base(
        PluginInitializer.Name)
    {
        this.windowSystem = windowSystem;
        this.dalamudServices = dalamudServices;
        this.serverConnection = serverConnection;
        this.mapChangeHandler = mapChangeHandler;
        this.attackManager = attackManager;
        this.configuration = configuration;
        this.logger = logger;
        this.xivChatSendLocations = Enum.GetNames<XivChatSendLocation>();
        this.falloffTypes = Enum.GetNames<AudioFalloffModel.FalloffType>();
        this.allLoggingLevels = [.. LogLevel.AllLoggingLevels.Select(l => l.Name)];
        windowSystem.AddWindow(this);
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
        if (ImGui.Begin(PluginInitializer.Name, ref this.visible))
        {
            DrawContents();
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
        using var tabs = ImRaii.TabBar("sp-tabs");
        if (!tabs) return;

        DrawMainTab();
        //DrawPublicTab();
        //DrawPrivateTab();
        //DrawConfigTab();
        //DrawMiscTab();
    }

    private void DrawMainTab()
    {
        using var mainTab = ImRaii.TabItem("Main");
        if (!mainTab) return;

        if (ImGui.Button("Clear All Attacks"))
        {
            this.attackManager.ClearAllAttacks();
        }

        if (ImGui.Button("Spawn Circle Omen"))
        {
            var player = this.dalamudServices.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<CircleOmen>(out var circle))
                {
                    circle.Set(new Position(player.Position));
                    circle.Set(new Rotation(player.Rotation));
                    circle.Set(new Scale(0.9f * Vector3.One));
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Spawn Twister"))
        {
            var player = this.dalamudServices.ClientState.LocalPlayer;
            if (player != null)
            {
                if (this.attackManager.TryCreateAttackEntity<Twister>(out var twister))
                {
                    twister.Set(new Position(player.Position));
                    twister.Set(new Rotation(player.Rotation));
                }
            }
        }

        if (ImGui.Button("Print Player Position"))
        {
            var player = this.dalamudServices.ClientState.LocalPlayer;
            if (player != null)
            {
                this.logger.Info("Player position: {0}", player.Position);
            }
        }
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
            var playerName = this.dalamudServices.ClientState.GetLocalPlayerFullName();
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
