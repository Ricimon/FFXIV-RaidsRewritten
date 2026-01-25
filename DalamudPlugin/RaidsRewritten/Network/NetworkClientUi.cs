using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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

        ImGui.SameLine();
        bool useCustomPartyId = configuration.UseCustomPartyId;
        if (ImGui.RadioButton("##UseCustomPartyId", useCustomPartyId))
        {
            configuration.UseCustomPartyId = !configuration.UseCustomPartyId;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use custom party ID.\nEnable this if custom mechanics need to be synced across non-party members.");
        }

        if (configuration.UseCustomPartyId)
        {
            string customPartyId = configuration.CustomPartyId;
            if (ImGui.InputText("Custom Party ID", ref customPartyId))
            {
                configuration.CustomPartyId = customPartyId;
                configuration.Save();
            }
            if (ImGui.IsItemHovered() && !ImGui.IsItemActive())
            {
                ImGui.SetTooltip("Treat this field like a password and share only with players that you expect to be connected with.");
            }
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
        pos += new Vector2(0.6f * h, -h / 2f);
        ImGui.SetCursorScreenPos(pos);

        using (ImRaii.Disabled(!client.IsConnected))
        {
            ImGui.Text($"Connected players: {client.ConnectedPlayersInParty}");
        }
    }
}
