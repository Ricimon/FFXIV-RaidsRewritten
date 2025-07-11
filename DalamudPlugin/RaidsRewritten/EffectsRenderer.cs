using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.UI.Util;
using RaidsRewritten.UI.View;

namespace RaidsRewritten;

public sealed class EffectsRenderer : IPluginUIView, IDisposable
{
    private class EffectTextEntry
    {
        public string Text { get; set; }
        public Vector2 Position { get; set; }

        public EffectTextEntry(string Text, Vector2 Position)
        {
            this.Text = Text;
            this.Position = Position;
        }

    }

    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    private readonly Lazy<EffectsRendererPresenter> presenter;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IAddonEventManager addonEventManager;
    private readonly IDataManager dataManager;
    private readonly KeyStateWrapper keyStateWrapper;
    private readonly Configuration configuration;
    private readonly MapManager mapManager;
    private readonly ILogger logger;
    private readonly EcsContainer ecsContainer;

    private readonly IFontHandle font;

    private const float PADDING_X = 10f;


    public EffectsRenderer(
        Lazy<EffectsRendererPresenter> presenter,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        IAddonLifecycle addonLifecycle,
        IAddonEventManager addonEventManager,
        IDataManager dataManager,
        KeyStateWrapper keyStateWrapper,
        Configuration configuration,
        MapManager mapManager,
        ILogger logger,
        EcsContainer ecsContainer)
    {
        this.presenter = presenter;
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
        this.addonLifecycle = addonLifecycle;
        this.addonEventManager = addonEventManager;
        this.dataManager = dataManager;
        this.keyStateWrapper = keyStateWrapper;
        this.configuration = configuration;
        this.mapManager = mapManager;
        this.logger = logger;
        this.ecsContainer = ecsContainer;

        this.font = pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansJpMedium, new()
                {
                    SizePx = 50
                });
            });
        });
    }

    public void Dispose()
    {
        font.Dispose();
    }

    public void Draw()
    {
        if (this.presenter == null) return;
        if (!this.font.Available) return;

        var toDraw = new List<EffectTextEntry>();
        var drawList = ImGui.GetForegroundDrawList();
        var maxWidth = 0f;
        var offsetY = 0f;

        var world = ecsContainer.World;

        using (font.Push())
        {
            // matches all conditions that exist in the world
            using var q = world.QueryBuilder<Scripts.Conditions.Condition.Component>().Build();
            q.Each((ref Scripts.Conditions.Condition.Component status) =>
            {
                AddStatus(toDraw, status.Name, Math.Round(status.TimeRemaining), ref offsetY, ref maxWidth);
            });

            if (offsetY > 0f)
            {
                var min = new Vector2(configuration.EffectsRendererPositionX - maxWidth / 2 - PADDING_X, configuration.EffectsRendererPositionY);
                var max = new Vector2(configuration.EffectsRendererPositionX + maxWidth / 2 + PADDING_X, configuration.EffectsRendererPositionY + offsetY);
                drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.3f)), 5);
                foreach (var effectEntry in toDraw)
                {
                    drawList.AddText(ImGui.GetFont(), 50, effectEntry.Position, Vector4Colors.Red.ToColorU32(), effectEntry.Text);
                }
            }
        }

    }

    private void AddStatus(List<EffectTextEntry> toDraw, string statusName, double timeRemaining, ref float offsetY, ref float maxWidth)
    {
        var text = $"{statusName} for {timeRemaining}s";
        var textSize = ImGui.CalcTextSize(text);
        var position = new Vector2(configuration.EffectsRendererPositionX - textSize.X / 2, configuration.EffectsRendererPositionY + offsetY);
        //drawList.AddText(ImGui.GetFont(), 50, position, Vector4Colors.Red.ToColorU32(), text);
        toDraw.Add(new EffectTextEntry(text, position));
        offsetY += textSize.Y;
        if (textSize.X > maxWidth) maxWidth = textSize.X;
    }
}
