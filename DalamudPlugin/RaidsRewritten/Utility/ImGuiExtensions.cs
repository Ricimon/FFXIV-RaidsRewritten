using ImGuiNET;

namespace RaidsRewritten.Extensions;

public static class ImGuiExtensions
{
    public static void SetDisabled(bool disabled = true)
    {
        ImGui.GetStyle().Alpha = disabled ? 0.5f : 1.0f;
    }

    public static void CaptureMouseThisFrame()
    {
        // Both lines are needed to consistently capture mouse input
        ImGui.GetIO().WantCaptureMouse = true;
        ImGui.SetNextFrameWantCaptureMouse(true);
    }
}
