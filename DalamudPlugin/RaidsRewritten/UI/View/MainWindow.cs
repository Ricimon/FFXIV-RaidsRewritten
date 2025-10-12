using System;
using System.Diagnostics;
using System.Numerics;
using System.Reactive.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.UI.Util;
using RaidsRewritten.Utility;
using Reactive.Bindings;
using ZLinq;

namespace RaidsRewritten.UI.View;

public sealed partial class MainWindow : Window, IPluginUIView, IDisposable
{
    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    public IReactiveProperty<bool> PrintLogsToChat { get; } = new ReactiveProperty<bool>();
    public IReactiveProperty<int> MinimumVisibleLogLevel { get; } = new ReactiveProperty<int>();

    private World World => this.ecsContainer.World;

    private readonly WindowSystem windowSystem;
    private readonly DalamudServices dalamud;
    private readonly EncounterManager encounterManager;
    private readonly EntityManager entityManager;
    private readonly Mechanic.Factory mechanicFactory;
    private readonly Configuration configuration;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;
    private readonly VfxSpawn vfxSpawn;
    private readonly Random random;
    private readonly ILogger logger;

    private readonly string windowName;
    private readonly string[] allLoggingLevels;

    public MainWindow(
        WindowSystem windowSystem,
        DalamudServices dalamud,
        EncounterManager encounterManager,
        EntityManager entityManager,
        Mechanic.Factory mechanicFactory,
        Configuration configuration,
        EcsContainer ecsContainer,
        CommonQueries commonQueries,
        VfxSpawn vfxSpawn,
        Random random,
        ILogger logger) : base(
        PluginInitializer.Name)
    {
        this.windowSystem = windowSystem;
        this.dalamud = dalamud;
        this.encounterManager = encounterManager;
        this.entityManager = entityManager;
        this.mechanicFactory = mechanicFactory;
        this.configuration = configuration;
        this.ecsContainer = ecsContainer;
        this.commonQueries = commonQueries;
        this.vfxSpawn = vfxSpawn;
        this.random = random;
        this.logger = logger;

        var version = GetType().Assembly.GetName().Version?.ToString() ?? string.Empty;
        this.windowName = PluginInitializer.Name;
        if (version.Length > 0)
        {
            var versionArray = version.Split(".");
            version = versionArray.AsValueEnumerable().Take(3).JoinToString(".");
            this.windowName += $" v{version}";
        }
#if DEBUG
        this.windowName += " (DEBUG)";
#endif
        this.allLoggingLevels = [.. LogLevel.AllLoggingLevels.AsValueEnumerable().Select(l => l.Name)];
        windowSystem.AddWindow(this);

        // Auto-position the effects renderer
        if (configuration.EffectsRendererPositionX == 0 && configuration.EffectsRendererPositionY == 0)
        {
            var viewport = ImGui.GetMainViewport();
            int x = (int)(viewport.Pos.X + viewport.Size.X / 2);
            int y = (int)(viewport.Pos.Y + viewport.Size.Y / 3);

            configuration.EffectsRendererPositionX = x;
            configuration.EffectsRendererPositionY = y;
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
            return;
        }

        var width = 350;
        ImGui.SetNextWindowSize(new Vector2(width, 400), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 250), new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin(this.windowName, ref this.visible))
        {
            this.World.DeferBegin();
            DrawContents();
            this.World.DeferEnd();
        }
        ImGui.End();
    }

    public void Dispose()
    {
        windowSystem.RemoveWindow(this);
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
                    this.entityManager.ClearAllManagedEntities();
                    this.World.DeleteWith<Condition.Component>();
                    this.World.DeleteWith<DelayedAction.Component>();
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
                        foreach (var mechanic in mechanics)
                        {
                            mechanic.Reset();
                        }
                    }
                }
            }
        }

        using var tabs = ImRaii.TabBar("rr-tabs");
        if (!tabs) return;

        DrawMainTab();
        DrawDebugTab();
        DrawMiscTab();
    }

    private void DrawMainTab()
    {
        using var mainTab = ImRaii.TabItem("Main");
        if (!mainTab) return;

        var effectsRendererPositionX = configuration.EffectsRendererPositionX;
        if (ImGui.InputInt("Position X", ref effectsRendererPositionX, 5))
        {
            configuration.EffectsRendererPositionX = effectsRendererPositionX;
            configuration.Save();
        }

        var effectsRendererPositionY = configuration.EffectsRendererPositionY;
        if (ImGui.InputInt("Position Y", ref effectsRendererPositionY, 5))
        {
            configuration.EffectsRendererPositionY = effectsRendererPositionY;
            configuration.Save();
        }

        if (ImGui.Button("Reset Status Placement"))
        {
            var viewport = ImGui.GetMainViewport();
            int x = (int)(viewport.Pos.X + viewport.Size.X / 2);
            int y = (int)(viewport.Pos.Y + viewport.Size.Y / 3);

            configuration.EffectsRendererPositionX = effectsRendererPositionX;
            configuration.EffectsRendererPositionY = effectsRendererPositionY;
            configuration.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button("Status Overlay"))
        {
            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
            {
                this.World.Entity().Set(new Condition.Component("test", 15.0f)).ChildOf(e);
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
#if !DEBUG
            using (ImRaii.Disabled(encounterManager.InCombat))
#endif
            {
                encounterManager.ActiveEncounter.DrawConfig();
            }
        }
    }

    private void DrawMiscTab()
    {
        using var miscTab = ImRaii.TabItem("Misc");
        if (!miscTab) return;

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
        ImGui.Text("Bugs or suggestions?\nWant to playtest the next Rewritten fight?");
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.35f, 0.40f, 0.95f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.41f, 0.45f, 1.0f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.32f, 0.36f, 0.88f, 1));
        if (ImGui.Button("Discord"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://discord.gg/rSucAJ6A7u", UseShellExecute = true });
        }
        ImGui.PopStyleColor(3);

        //ImGui.SameLine();
        //ImGui.Text("|");
        //ImGui.SameLine();

        //ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.39f, 0.20f, 1));
        //ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.49f, 0.30f, 1));
        //ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.92f, 0.36f, 0.18f, 1));
        //if (ImGui.Button("Support on Ko-fi"))
        //{
        //    Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/ricimon", UseShellExecute = true });
        //}
        //ImGui.PopStyleColor(3);
    }
}
