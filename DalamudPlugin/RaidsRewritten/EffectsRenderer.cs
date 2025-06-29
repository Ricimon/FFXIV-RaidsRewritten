using System;
using System.Collections.Generic;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using RaidsRewritten.Extensions;
using RaidsRewritten.Input;
using RaidsRewritten.Log;
using RaidsRewritten.UI.Util;
using RaidsRewritten.UI.View;

namespace RaidsRewritten;

public class EffectsRenderer : IPluginUIView, IDisposable
{
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
    private readonly IFontHandle font;
    private readonly OrderedDictionary<int, string> displayText = new();

    private int displayTextId = 0;

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
        ILogger logger)
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
        this.font = pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => {
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
        GC.SuppressFinalize(this);
    }

    public void Draw()
    {
        if (this.presenter == null) return;
        if (!this.font.Available) return;

        var drawList = ImGui.GetForegroundDrawList();
        var offsetY = 0f;

        // font.Push() just to convert IFontHandle to ImFontPtr
        using (font.Push())
        {
            foreach(var text in displayText.Values)
            {
                var textSize = ImGui.CalcTextSize(text);
                var position = new System.Numerics.Vector2(configuration.EffectsRendererPositionX - textSize.X / 2, configuration.EffectsRendererPositionY + offsetY);
                drawList.AddText(ImGui.GetFont(), 50, position, Vector4Colors.Red.ToColorU32(), text);
                offsetY += textSize.Y;
            }
        }
    }

    public int AddText(string text)
    {
        displayText[displayTextId] = text;
        return displayTextId++;
    }

    public bool ModifyText(int id, string text)
    {
        if (id < 0 && id >= displayText.Count) return false;
        displayText[id] = text;
        return true;
    }

    public bool RemoveText(int id)
    {
        if (id < 0) return false;
        return displayText.Remove(id);
    }
}
