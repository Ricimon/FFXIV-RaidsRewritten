using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Bindings.ImGui;
using RaidsRewritten.UI.Util;

namespace RaidsRewritten.Network;

public class NetworkClientUi(NetworkClient client, Configuration configuration)
{
    public void DrawConfig()
    {
        string serverUrl = client.GetServerUrl();
        if (ImGui.InputText("Server URL", ref serverUrl))
        {
            configuration.ServerUrl = serverUrl;
            configuration.Save();
        }

        var buttonWidth = ImGui.CalcTextSize("Disconnect").X + 2 * ImGui.GetStyle().CellPadding.X;
        if (client.IsConnected || client.IsConnecting)
        {
            if (ImGui.Button("Disconnect", new(buttonWidth, 0)))
            {
                client.DisconnectAsync().SafeFireAndForget();
            }
        }
        else
        {
            if (ImGui.Button("Connect", new(buttonWidth, 0)))
            {
                client.Connect();
            }
        }

        Vector4 color = Vector4Colors.Red;
        string tooltip = "Not Connected";
        if (client.IsConnected)
        {
            color = Vector4Colors.Green;
            tooltip = "Connected";
        }
        else if (client.IsConnecting)
        {
            color = Vector4Colors.Orange;
            tooltip = "Connecting";
        }

        ImGui.SameLine();
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().FramePadding.Y;

        // Connectivity/activity indicator
        var radius = 0.3f * h;
        pos += new Vector2(0.25f * h, h / 2f);
        drawList.AddCircleFilled(pos, radius, ImGui.ColorConvertFloat4ToU32(color));
        // Tooltip
        if (Vector2.Distance(ImGui.GetMousePos(), pos) < radius)
        {
            ImGui.SetTooltip(tooltip);
        }
        pos += new Vector2(radius + 3, -h / 2.25f);
        ImGui.SetCursorScreenPos(pos);
        ImGui.NewLine();
    }
}
