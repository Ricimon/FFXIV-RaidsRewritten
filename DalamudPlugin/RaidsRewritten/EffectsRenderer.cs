using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Flecs.NET.Core;
using ImGuiNET;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
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

    private class EffectGaugeEntry
    { 
        public Vector2 Position { get; set; }
        public Vector2 Offset { get; set; }
        public Vector2 BarSize { get; set; }
        public Vector2 ImageSize { get; set; }
        public string Path { get; set; }
        public float Value { get; set; }
        public EffectGaugeEntry(Vector2 Position, Vector2 Offset, Vector2 BarSize, Vector2 ImageSize, string Path, float Value)
        { 
            this.Position = Position;
            this.Offset = Offset;
            this.BarSize = BarSize;
            this.ImageSize = ImageSize;
            this.Path = Path;
            this.Value = Value;
        }
    }

    private class EffectRectEntry
    { 
        public Vector2 Position { get; set; }

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
    private readonly Query<Condition.Component> componentsQuery;
    private readonly Query<Temperature.Component> temperatureQuery;

    private const float PADDING_X = 10f;
    private const float PADDING_Y = 10f;

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

        this.componentsQuery = ecsContainer.World.QueryBuilder<Condition.Component>().Without<Condition.Hidden>().Cached().Build();
        this.temperatureQuery = ecsContainer.World.QueryBuilder<Temperature.Component>().Cached().Build();
    }

    public void Dispose()
    {
        this.font.Dispose();
        this.componentsQuery.Dispose();
    }

    public void Draw()
    {
        if (this.presenter == null) return;
        if (!this.font.Available) return;

        var toDraw = new List<EffectTextEntry>();
        var toGaugeDraw = new List<EffectGaugeEntry>();

        var drawList = ImGui.GetForegroundDrawList();
        var maxWidth = 0f;
        var offsetY = 0f;

        var world = ecsContainer.World;

        using (font.Push())
        {
            // matches all conditions that exist in the world
            this.componentsQuery.Each((ref Condition.Component status) =>
            {
                AddStatus(toDraw, status.Name, Math.Round(status.TimeRemaining), ref offsetY, ref maxWidth);
            });

            this.temperatureQuery.Each((ref Temperature.Component temperature) => { 
                AddTemperature(toGaugeDraw, temperature);
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

            foreach (var gaugeEntry in toGaugeDraw)
            {
                var imgGauge = this.textureProvider.GetFromFile(this.pluginInterface.GetResourcePath(gaugeEntry.Path)).GetWrapOrDefault()?.ImGuiHandle ?? default;
                drawList.AddImage(imgGauge, gaugeEntry.Position, gaugeEntry.Position + gaugeEntry.ImageSize);

                float clampedValue = Math.Clamp(gaugeEntry.Value, -100f, 100f);
                float normalized = (clampedValue + 100f) / 200f;
                Vector4 barColor;
                string barText;
                if (clampedValue == -100f)
                {
                    barColor = new Vector4(0.6f, 1, 1, 0.5f);
                }
                else if (clampedValue < 0)
                {
                    barColor = new Vector4(0, 0, 1, 0.5f);
                }
                else
                {
                    barColor = new Vector4(1, 1, 0, 0.5f);
                }
                
                Vector2 barPosition = gaugeEntry.Position + gaugeEntry.Offset;
                float fillWidth = normalized * gaugeEntry.BarSize.X;
                drawList.AddRectFilled(barPosition + new Vector2(gaugeEntry.BarSize.X / 2, 0), barPosition + new Vector2(fillWidth, gaugeEntry.BarSize.Y), ImGui.ColorConvertFloat4ToU32(barColor), 0f);

                if (gaugeEntry.Value > 100f)
                {
                    barColor = new Vector4(1, 0, 0, 0.5f);
                    float overflowNormalized = gaugeEntry.Value / 200f;
                    fillWidth = overflowNormalized * gaugeEntry.BarSize.X;

                    drawList.AddRectFilled(barPosition + new Vector2(gaugeEntry.BarSize.X / 2, 0), barPosition + new Vector2(fillWidth, gaugeEntry.BarSize.Y), ImGui.ColorConvertFloat4ToU32(barColor), 0f);
                }


                if (gaugeEntry.Value > 100)
                {
                    barText = $"+{gaugeEntry.Value}!";
                }
                else if (gaugeEntry.Value > 0)
                {
                    barText = $"+{gaugeEntry.Value}";
                }
                else
                {
                    barText = $"{gaugeEntry.Value}";
                }
                var textSize = ImGui.CalcTextSize(barText);
                var position = new Vector2(barPosition.X + fillWidth - (textSize.X/2), barPosition.Y + textSize.Y / 2 + PADDING_Y);

                TextOutline(ImGui.GetFont(), 40, position, Vector4Colors.Black.ToColorU32(), barText, 2, drawList);
                drawList.AddText(ImGui.GetFont(), 40, position, Vector4Colors.White.ToColorU32(), barText);
            }

        }

    }

    private void TextOutline(ImFontPtr font, int fontSize, Vector2 position, UInt32 color, string text, int outline, ImDrawListPtr drawListPtr)
    {
        drawListPtr.AddText(font, fontSize, position + new Vector2(-outline, -outline), color, text);
        drawListPtr.AddText(font, fontSize, position + new Vector2(outline, -outline), color, text);
        drawListPtr.AddText(font, fontSize, position + new Vector2(-outline, outline), color, text);
        drawListPtr.AddText(font, fontSize, position + new Vector2(outline, outline), color, text);
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

    private void AddTemperature(List<EffectGaugeEntry> toDraw, Temperature.Component tc) 
    {
        Vector2 offset = new Vector2(64, 79);
        Vector2 barSize = new Vector2(370, 24);
        Vector2 imageSize = new Vector2(498, 147);
        Vector2 position = new Vector2(configuration.GetEncounterSetting(Temperature.GaugeXPositionConfig, 0) - imageSize.X / 2, configuration.GetEncounterSetting(Temperature.GaugeYPositionConfig, 0) - imageSize.Y / 2);
        var path = Temperature.GaugeImagePath;
        toDraw.Add(new EffectGaugeEntry(position, offset, barSize, imageSize, path, tc.CurrentTemperature));
    }
}
