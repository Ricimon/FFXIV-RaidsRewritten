using System;
using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using RaidsRewritten.Audio;
using RaidsRewritten.Data;
using RaidsRewritten.Extensions;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.Network;
using RaidsRewritten.UI.View;
using Reactive.Bindings;

namespace RaidsRewritten.UI.Presenter;

public class MainWindowPresenter(
    MainWindow view,
    Configuration configuration,
    IClientState clientState,
    IFramework framework,
    IDataManager dataManager,
    IAudioDeviceController audioDeviceController,
    ServerConnection serverConnection,
    KeyStateWrapper keyStateWrapper,
    XivHudNodeMap hudNodeMap,
    ILogger logger) : IPluginUIPresenter
{
    public IPluginUIView View => this.view;

    private readonly MainWindow view = view;
    private readonly Configuration configuration = configuration;
    private readonly IClientState clientState = clientState;
    private readonly IFramework framework = framework;
    private readonly IDataManager dataManager = dataManager;
    private readonly IAudioDeviceController audioDeviceController = audioDeviceController;
    private readonly ServerConnection serverConection = serverConnection;
    private readonly KeyStateWrapper keyStateWrapper = keyStateWrapper;
    private readonly XivHudNodeMap hudNodeMap = hudNodeMap;
    private readonly ILogger logger = logger;

    private bool keyDownListenerSubscribed;

    public void SetupBindings()
    {
        BindVariables();
        BindActions();
    }

    private void BindVariables()
    {
        Bind(this.view.PublicRoom,
            b =>
            {
                this.configuration.PublicRoom = b; this.configuration.Save();
            },
            this.configuration.PublicRoom);
        Bind(this.view.RoomName,
            s => { this.configuration.RoomName = s; this.configuration.Save(); }, this.configuration.RoomName);
        Bind(this.view.RoomPassword,
            s => { this.configuration.RoomPassword = s; this.configuration.Save(); }, this.configuration.RoomPassword);

        Bind(this.view.MasterVolume,
            f => { this.configuration.MasterVolume = f; this.configuration.Save(); }, this.configuration.MasterVolume);

        Bind(this.view.PlayRoomJoinAndLeaveSounds,
            b => { this.configuration.PlayRoomJoinAndLeaveSounds = b; this.configuration.Save(); }, this.configuration.PlayRoomJoinAndLeaveSounds);
        Bind(this.view.KeybindsRequireGameFocus,
            b => { this.configuration.KeybindsRequireGameFocus = b; this.configuration.Save(); }, this.configuration.KeybindsRequireGameFocus);
        Bind(this.view.PrintLogsToChat,
            b => { this.configuration.PrintLogsToChat = b; this.configuration.Save(); }, this.configuration.PrintLogsToChat);
        Bind(this.view.MinimumVisibleLogLevel,
            i => { this.configuration.MinimumVisibleLogLevel = i; this.configuration.Save(); }, this.configuration.MinimumVisibleLogLevel);
    }

    private void BindActions()
    {
        this.view.JoinRoom.Subscribe(_ =>
        {
            this.serverConection.Channel?.ClearLatestDisconnectMessage();
            if (this.view.PublicRoom.Value)
            {
                this.serverConection.JoinPublicRoom();
            }
            else
            {
                if (string.IsNullOrEmpty(this.view.RoomName.Value))
                {
                    var playerName = this.clientState.GetLocalPlayerFullName();
                    if (playerName == null)
                    {
                        this.logger.Error("Player name is null, cannot autofill private room name.");
                        return;
                    }
                    this.view.RoomName.Value = playerName;
                }
                this.serverConection.JoinPrivateRoom(this.view.RoomName.Value, this.view.RoomPassword.Value);
            }
        });

        this.view.LeaveRoom.Subscribe(_ => this.serverConection.LeaveRoom(false).SafeFireAndForget(ex => this.logger.Error(ex.ToString())));

        this.view.KeybindBeingEdited.Subscribe(k =>
        {
            if (k != Keybind.None && !this.keyDownListenerSubscribed)
            {
                this.keyStateWrapper.OnKeyDown += OnKeyDown;
                this.keyDownListenerSubscribed = true;
            }
            else if (k == Keybind.None && this.keyDownListenerSubscribed)
            {
                this.keyStateWrapper.OnKeyDown -= OnKeyDown;
                this.keyDownListenerSubscribed = false;
            }
        });
        this.view.ClearKeybind.Subscribe(k =>
        {
            switch (k)
            {
                default:
                    return;
            }
            this.configuration.Save();
        });

        this.view.PrintNodeMap1.Subscribe(_ =>
        {
            foreach (var n in this.hudNodeMap.CollisionNodeMap)
            {
                this.logger.Info("Node {0} -> {1}:{2}", n.Key.ToString("X"), n.Value.HudSection, n.Value.Index);
            }
        });
        this.view.PrintNodeMap2.Subscribe(_ =>
        {
            foreach (var n in this.hudNodeMap.ElementNodeMap)
            {
                this.logger.Info("HudSection {0} -> {1}", n.Key.HudSection, n.Value.ToString("X"));
            }
        });
    }

    private void Bind<T>(
        IReactiveProperty<T> reactiveProperty,
        Action<T> dataUpdateAction,
        T initialValue)
    {
        if (initialValue != null)
        {
            reactiveProperty.Value = initialValue;
        }
        reactiveProperty.Subscribe(dataUpdateAction);
    }

    private void OnKeyDown(VirtualKey key)
    {
        // Disallow any keybinds to left mouse
        if (key == VirtualKey.LBUTTON) { return; }

        // This callback can be called from a non-framework thread, and UI values should only be modified
        // on the framework thread (or else the game can crash)
        this.framework.Run(() =>
        {
            var editedKeybind = this.view.KeybindBeingEdited.Value;
            this.view.KeybindBeingEdited.Value = Keybind.None;

            switch (editedKeybind)
            {
                default:
                    return;
            }
            this.configuration.Save();
        });
    }

}
