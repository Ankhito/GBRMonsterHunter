using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Huntsman.UI;

internal static class HuntsmanTheme
{
    public static readonly Vector4 Gold = new(0.20f, 0.56f, 0.96f, 1f);
    public static readonly Vector4 GoldSoft = new(0.38f, 0.68f, 1.00f, 1f);
    public static readonly Vector4 Black = new(0.082f, 0.082f, 0.082f, 0.94f);
    public static readonly Vector4 Panel = new(0.120f, 0.120f, 0.120f, 0.92f);
    public static readonly Vector4 PanelSoft = new(0.160f, 0.160f, 0.160f, 1f);
    public static readonly Vector4 BorderGold = new(0.20f, 0.56f, 0.96f, 0.28f);
    public static readonly Vector4 Text = new(0.960f, 0.960f, 0.960f, 1f);
    public static readonly Vector4 Dimmed = new(0.58f, 0.56f, 0.55f, 1f);
    public static readonly Vector4 Green = new(0.36f, 0.82f, 0.45f, 1f);
    public static readonly Vector4 Red = new(0.96f, 0.42f, 0.42f, 1f);
    public static readonly Vector4 Warn = Gold;

    private const int ThemeColors = 29;
    private const int ThemeVars = 9;

    public static void Push()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f));

        Color(ImGuiCol.Text, Text);
        Color(ImGuiCol.TextDisabled, Dimmed);
        Color(ImGuiCol.WindowBg, Black);
        Color(ImGuiCol.ChildBg, Panel);
        Color(ImGuiCol.PopupBg, new Vector4(0.100f, 0.100f, 0.100f, 0.96f));
        Color(ImGuiCol.Border, BorderGold);
        Color(ImGuiCol.FrameBg, PanelSoft);
        Color(ImGuiCol.FrameBgHovered, new Vector4(0.205f, 0.213f, 0.230f, 1f));
        Color(ImGuiCol.FrameBgActive, new Vector4(0.255f, 0.270f, 0.300f, 1f));
        Color(ImGuiCol.TitleBg, new Vector4(0.100f, 0.100f, 0.100f, 1f));
        Color(ImGuiCol.TitleBgActive, new Vector4(0.090f, 0.170f, 0.280f, 1f));
        Color(ImGuiCol.TitleBgCollapsed, new Vector4(0.100f, 0.100f, 0.100f, 0.75f));
        Color(ImGuiCol.Button, PanelSoft);
        Color(ImGuiCol.ButtonHovered, new Vector4(0.180f, 0.390f, 0.640f, 1f));
        Color(ImGuiCol.ButtonActive, new Vector4(0.200f, 0.560f, 0.960f, 1f));
        Color(ImGuiCol.Header, new Vector4(0.180f, 0.190f, 0.205f, 1f));
        Color(ImGuiCol.HeaderHovered, new Vector4(0.170f, 0.370f, 0.610f, 1f));
        Color(ImGuiCol.HeaderActive, new Vector4(0.200f, 0.500f, 0.860f, 1f));
        Color(ImGuiCol.CheckMark, Gold);
        Color(ImGuiCol.SliderGrab, GoldSoft);
        Color(ImGuiCol.SliderGrabActive, Gold);
        Color(ImGuiCol.Separator, new Vector4(0.240f, 0.240f, 0.240f, 1f));
        Color(ImGuiCol.SeparatorHovered, new Vector4(0.200f, 0.560f, 0.960f, 0.70f));
        Color(ImGuiCol.Tab, Panel);
        Color(ImGuiCol.TabHovered, new Vector4(0.180f, 0.390f, 0.640f, 1f));
        Color(ImGuiCol.TabActive, new Vector4(0.130f, 0.260f, 0.420f, 1f));
        Color(ImGuiCol.ScrollbarBg, new Vector4(0.080f, 0.080f, 0.080f, 0.60f));
        Color(ImGuiCol.ScrollbarGrab, new Vector4(0.240f, 0.240f, 0.240f, 1f));
        Color(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.180f, 0.390f, 0.640f, 1f));
    }

    public static void Pop()
    {
        ImGui.PopStyleColor(ThemeColors);
        ImGui.PopStyleVar(ThemeVars);
    }

    private static void Color(ImGuiCol idx, Vector4 color) => ImGui.PushStyleColor(idx, color);
}
