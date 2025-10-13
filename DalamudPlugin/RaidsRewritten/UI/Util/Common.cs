using Dalamud.Bindings.ImGui;

namespace RaidsRewritten.UI.Util; 

public class Common 
{
    public static void HelpMarker(string description) 
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) 
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static bool CenteredButton(string label)
    {
        var style = ImGui.GetStyle();

        var size = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2.0f;
        var avail = ImGui.GetContentRegionAvail().X;

        var off = (avail - size) * 0.5f;
        if (off > 0.0f)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);
        }

        return ImGui.Button(label);
    }
}