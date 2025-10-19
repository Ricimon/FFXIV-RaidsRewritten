using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RaidsRewritten.UI.View;

public class ChangelogWindow : IPluginUIView
{
    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    private readonly string windowName = $"{PluginInitializer.Name} Changelog##Changelog";

    public void Draw()
    {
        if (!Visible) { return; }

        var width = Math.Min(ImGui.GetMainViewport().Size.X / ImGuiHelpers.GlobalScale / 2, 500);
        var height = Math.Min(ImGui.GetMainViewport().Size.Y / ImGuiHelpers.GlobalScale / 2, 500);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Appearing);
        if (ImGui.Begin(this.windowName, ref this.visible))
        {
            DrawContents();
        }
        ImGui.End();
    }

    private void DrawContents()
    {
        using (var child = ImRaii.Child("Entries", new Vector2(-1, -ImGui.GetFrameHeight() * 1.5f)))
        {
            if (child)
            {
                ImGui.Text("v1.0.1");
                using (var i = ImRaii.PushIndent())
                {
                    ImGui.TextWrapped("Added scaling support to temperature gauge UI." +
                        "\nBugfix Dreadknight desync.");
                }

                ImGui.Text("v1.0.0");
                using (var i = ImRaii.PushIndent())
                {
                    ImGui.TextWrapped("Initial release! UCOB Rewritten available.");
                }
            }
        }

        //ImGui.Spacing();
        if (ImGui.Button("Close",
            new Vector2(ImGui.GetWindowWidth() - 2 * ImGui.GetStyle().WindowPadding.X, 0)))
        {
            this.visible = false;
        }
    }
}
