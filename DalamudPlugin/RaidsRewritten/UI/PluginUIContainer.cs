using System;
using Dalamud.Interface.Windowing;
using RaidsRewritten.Log;
using RaidsRewritten.UI.View;
using RaidsRewritten.Utility;

namespace RaidsRewritten.UI;

// It is good to have this be disposable in general, in case you ever need it
// to do any cleanup
public sealed class PluginUIContainer(
    IPluginUIView[] pluginUiViews,
    MainWindow mainWindow,
    DalamudServices dalamud,
    WindowSystem windowSystem,
    ILogger logger) : IDalamudHook
{
    public void Dispose()
    {
        dalamud.PluginInterface.UiBuilder.Draw -= Draw;
        dalamud.PluginInterface.UiBuilder.OpenMainUi -= ShowMainWindow;
    }

    public void HookToDalamud()
    {
        dalamud.PluginInterface.UiBuilder.Draw += Draw;
        dalamud.PluginInterface.UiBuilder.OpenMainUi += ShowMainWindow;
    }

    public void Draw()
    {
        // This is our only draw handler attached to UIBuilder, so it needs to be
        // able to draw any windows we might have open.
        // Each method checks its own visibility/state to ensure it only draws when
        // it actually makes sense.
        // There are other ways to do this, but it is generally best to keep the number of
        // draw delegates as low as possible.

        try
        {
            foreach (var view in pluginUiViews)
            {
                view.Draw();
            }
        }
        catch (Exception e)
        {
            logger.Error(e.ToStringFull());
        }
    }

    private void ShowMainWindow()
    {
        mainWindow.Visible = true;
    }
}
