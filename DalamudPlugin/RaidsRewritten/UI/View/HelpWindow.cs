using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.UI.View;

public class HelpWindow(DalamudServices dalamud, ILogger logger) : IPluginUIView
{
    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    private readonly string windowName = $"{PluginInitializer.Name} Help##Help";

    public void Draw()
    {
        if (!Visible) { return; }

        ImGui.SetNextWindowSize(Vector2.Zero, ImGuiCond.Always);
        var width = Math.Min(ImGui.GetMainViewport().Size.X / ImGuiHelpers.GlobalScale / 2, 800);
        ImGui.SetNextWindowSizeConstraints(
            new(width, 0),
            new(width, float.PositiveInfinity));
        if (ImGui.Begin(this.windowName, ref this.visible, ImGuiWindowFlags.NoResize))
        {
            DrawContents();
        }
        ImGui.End();
    }

    private void DrawContents()
    {
        var iconPath = dalamud.PluginInterface.GetResourcePath("icon.png");
        var icon = dalamud.TextureProvider.GetFromFile(iconPath).GetWrapOrDefault();
        if (icon != null)
        {
            ImGui.Image(icon.Handle, ImGuiHelpers.GlobalScale * new Vector2(100, 100));
        }
        ImGui.TextWrapped("RaidsRewritten is a plugin that adds custom mechanics to fights.");

        ImGui.Text("---");
        ImGui.TextWrapped("In case the plugin is accidentally running when not intended, there is an emergency shutoff button.");

        ImGui.Text("---");
        ImGui.TextWrapped("Custom mechanics can apply custom statuses that may affect the control of your character.");
        ImGui.TextWrapped("These custom statuses will appear on your status bar and on the party list.");
        ImGui.TextWrapped("All custom statuses will have a chef-hat icon with a (RaidsRewritten) description to indicate that they come from the plugin.");
        ImGui.TextWrapped("You can swap back to legacy status rendering from RaidsRewritten v1 through the Misc menu.");

        ImGui.Text("---");
        ImGui.TextWrapped("The plugin becomes active when in a supported fight.");
        ImGui.TextWrapped("Custom mechanics can be customized before a pull, but not during a pull.");
        ImGui.TextWrapped("The intended difficulty is a specially crafted set of custom mechanics aimed at making the fight much harder and a novel progression experience.");
        ImGui.TextWrapped("Some custom mechanic names are intentionally unclear because I like to be funny.");
        ImGui.TextWrapped("Some custom mechanics use client-side RNG seeding, so it's important to match the RNG seed between all players so every player sees the same mechanic variation for mechanics with RNG.");

        ImGui.Text("---");
        ImGui.TextWrapped("Custom mechanic punishments don't do anything that you can't normally do without the plugin, and you can test some of their effects in the Debug tab.");
        ImGui.TextWrapped("You don't need to pretend mechanic punishments are worse than they actually are. Be creative with solutions and recoveries!");

        ImGui.Text("---");
        ImGui.TextWrapped("Lastly, there is a special reward if you can defeat the intended difficulty (or harder)!");

        ImGui.Spacing();
        if (ImGui.Button("Close",
            new Vector2(ImGui.GetWindowWidth() - 2 * ImGui.GetStyle().WindowPadding.X, 0)))
        {
            this.visible = false;
        }
    }
}
