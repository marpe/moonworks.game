using Mochi.DearImGui;

namespace MyGame.TWImGui;

public static unsafe class ImGuiThemes
{
    public static void DefaultDarkTheme()
    {
        ImGui.StyleColorsDark();
    }

    public static void DefaultClassic()
    {
        ImGui.StyleColorsClassic();
    }

    public static void DarkTheme()
    {
        var style = ImGui.GetStyle();
        
        style->Colors[(int)ImGuiCol.Text] = new Num.Vector4(0.83f, 0.83f, 0.83f, 1.00f);
        style->Colors[(int)ImGuiCol.TextDisabled] = new Num.Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        style->Colors[(int)ImGuiCol.WindowBg] = new Num.Vector4(20 / 255f, 20 / 255f, 20 / 255f, 1.00f);
        style->Colors[(int)ImGuiCol.ChildBg] = new Num.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style->Colors[(int)ImGuiCol.PopupBg] = new Num.Vector4(0.19f, 0.19f, 0.19f, 0.92f);
        style->Colors[(int)ImGuiCol.Border] = new Num.Vector4(0.2f, 0.2f, 0.2f, 1.00f); // new Num.Vector4(0.28f, 0.28f, 0.28f, 1.00f)
        style->Colors[(int)ImGuiCol.BorderShadow] = new Num.Vector4(0.00f, 0.00f, 0.00f, 0); //0.55f);
        style->Colors[(int)ImGuiCol.FrameBg] = new Num.Vector4(0.14f, 0.14f, 0.14f, 1.0f); //  new Num.Vector4(41 / 255f, 41 / 255f, 41 / 255f, 1.0f) // new Num.Num.Vector4(0.05f, 0.05f, 0.05f, 0.70f);
        style->Colors[(int)ImGuiCol.FrameBgHovered] = new Num.Vector4(0.19f, 0.19f, 0.19f, 1f);
        style->Colors[(int)ImGuiCol.FrameBgActive] = new Num.Vector4(0.19f, 0.19f, 0.19f, 1f);
        style->Colors[(int)ImGuiCol.TitleBg] = new Num.Vector4(0.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.TitleBgActive] = new Num.Vector4(0.06f, 0.06f, 0.06f, 1.00f);
        style->Colors[(int)ImGuiCol.TitleBgCollapsed] = new Num.Vector4(0.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.MenuBarBg] = new Num.Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        style->Colors[(int)ImGuiCol.ScrollbarBg] = new Num.Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style->Colors[(int)ImGuiCol.ScrollbarGrab] = new Num.Vector4(0.34f, 0.34f, 0.34f, 0.54f);
        style->Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Num.Vector4(0.40f, 0.40f, 0.40f, 0.54f);
        style->Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Num.Vector4(0.56f, 0.56f, 0.56f, 0.54f);
        style->Colors[(int)ImGuiCol.CheckMark] = new Num.Vector4(2 / 255f, 115f / 255f, 235f / 255f, 255f / 255f); //new Num.Num.Vector4(0.33f, 0.67f, 0.86f, 1.00f);
        style->Colors[(int)ImGuiCol.SliderGrab] = new Num.Vector4(0.34f, 0.34f, 0.34f, 0.54f);
        style->Colors[(int)ImGuiCol.SliderGrabActive] = new Num.Vector4(0.56f, 0.56f, 0.56f, 0.54f);
        style->Colors[(int)ImGuiCol.Button] =
            new Num.Vector4(0, 0.34f, 0.69f,
                1); // new Num.Num.Vector4(2 / 255f, 115f / 255f, 235f / 255f, 255f / 255f); // new Num.Num.Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style->Colors[(int)ImGuiCol.ButtonHovered] = new Num.Vector4(0.1f, 0.32f, 0.62f, 1); // new Num.Num.Vector4(0.19f, 0.19f, 0.19f, 0.54f);
        style->Colors[(int)ImGuiCol.ButtonActive] = new Num.Vector4(0.09f, 0.29f, 0.51f, 1f); // new Num.Num.Vector4(0.20f, 0.22f, 0.23f, 1.00f);
        style->Colors[(int)ImGuiCol.Header] = new Num.Vector4(23 / 255f, 65 / 255f, 118 / 255f, 255 / 255f);
        style->Colors[(int)ImGuiCol.HeaderHovered] =
            new Num.Vector4(23 / 255f, 65 / 255f, 118 / 255f, 255 / 255f); // new Num.Num.Vector4(45 / 255f, 45 / 255f, 45 / 255f, 255 / 255f);
        style->Colors[(int)ImGuiCol.HeaderActive] = new Num.Vector4(23 / 255f, 65 / 255f, 118 / 255f, 255 / 255f);
        style->Colors[(int)ImGuiCol.Separator] = new Num.Vector4(0.28f, 0.28f, 0.28f, 0.29f);
        style->Colors[(int)ImGuiCol.SeparatorHovered] = new Num.Vector4(0.44f, 0.44f, 0.44f, 0.29f);
        style->Colors[(int)ImGuiCol.SeparatorActive] = new Num.Vector4(0.40f, 0.44f, 0.47f, 1.00f);
        style->Colors[(int)ImGuiCol.ResizeGrip] = new Num.Vector4(0.28f, 0.28f, 0.28f, 0.29f);
        style->Colors[(int)ImGuiCol.ResizeGripHovered] = new Num.Vector4(0.44f, 0.44f, 0.44f, 0.29f);
        style->Colors[(int)ImGuiCol.ResizeGripActive] = new Num.Vector4(0.40f, 0.44f, 0.47f, 1.00f);
        style->Colors[(int)ImGuiCol.Tab] = new Num.Vector4(0.161f, 0.161f, 0.161f, 1.000f); //new Num.Num.Vector4(0.00f, 0.00f, 0.00f, 0.52f);
        style->Colors[(int)ImGuiCol.TabHovered] = new Num.Vector4(0.094f, 0.094f, 0.094f, 1.000f); //new Num.Num.Vector4(0.1f, 0.2f, 0.25f, 1.00f);
        style->Colors[(int)ImGuiCol.TabActive] = new Num.Vector4(0.07f, 0.28f, 0.55f, 1.00f); //new Num.Num.Vector4(0.19f, 0.38f, 0.5f, 1.00f);
        style->Colors[(int)ImGuiCol.TabUnfocused] = new Num.Vector4(0.094f, 0.094f, 0.094f, 1.000f); //new Num.Num.Vector4(0.00f, 0.00f, 0.00f, 0.52f);
        style->Colors[(int)ImGuiCol.TabUnfocusedActive] = new Num.Vector4(0.19f, 0.20f, 0.22f, 1.00f); //new Num.Num.Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        style->Colors[(int)ImGuiCol.DockingPreview] = new Num.Vector4(0.33f, 0.67f, 0.86f, 1.00f);
        style->Colors[(int)ImGuiCol.DockingEmptyBg] = new Num.Vector4(1.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.PlotLines] = new Num.Vector4(1.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.PlotLinesHovered] = new Num.Vector4(1.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.PlotHistogram] = new Num.Vector4(1.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.PlotHistogramHovered] = new Num.Vector4(1.00f, 0.00f, 0.00f, 1.00f);
        style->Colors[(int)ImGuiCol.TableHeaderBg] = new Num.Vector4(0.00f, 0.00f, 0.00f, 0.52f);
        style->Colors[(int)ImGuiCol.TableBorderStrong] = new Num.Vector4(0.26f, 0.26f, 0.26f, 1.00f);
        style->Colors[(int)ImGuiCol.TableBorderLight] = new Num.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
        style->Colors[(int)ImGuiCol.TableRowBg] = new Num.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style->Colors[(int)ImGuiCol.TableRowBgAlt] = new Num.Vector4(1.00f, 1.00f, 1.00f, 0.06f);
        style->Colors[(int)ImGuiCol.TextSelectedBg] = new Num.Vector4(0.20f, 0.22f, 0.23f, 1.00f);
        style->Colors[(int)ImGuiCol.DragDropTarget] = new Num.Vector4(0.33f, 0.67f, 0.86f, 1.00f);
        style->Colors[(int)ImGuiCol.NavHighlight] = new Num.Vector4(0.26f, 0.59f, 0.98f, 1.00f);
        style->Colors[(int)ImGuiCol.NavWindowingHighlight] = new Num.Vector4(1.00f, 1.00f, 1.00f, 0.70f);
        style->Colors[(int)ImGuiCol.NavWindowingDimBg] = new Num.Vector4(0.80f, 0.80f, 0.80f, 0.20f);
        style->Colors[(int)ImGuiCol.ModalWindowDimBg] = new Num.Vector4(0.80f, 0.80f, 0.80f, 0.35f);

        style->WindowPadding = new Num.Vector2(8.00f, 8.00f);
        style->FramePadding = new Num.Vector2(5.00f, 2.00f);
        style->CellPadding = new Num.Vector2(6.00f, 6.00f);
        style->ItemSpacing = new Num.Vector2(6.00f, 6.00f);
        style->ItemInnerSpacing = new Num.Vector2(6.00f, 6.00f);
        style->TouchExtraPadding = new Num.Vector2(0.00f, 0.00f);
        style->IndentSpacing = 10;
        style->ScrollbarSize = 15;
        style->GrabMinSize = 10;
        style->WindowBorderSize = 1;
        style->ChildBorderSize = 1;
        style->PopupBorderSize = 1;
        style->FrameBorderSize = 1;//0;
        style->TabBorderSize = 1;
        style->WindowRounding = 0; // 7
        style->ChildRounding = 4;
        style->FrameRounding = 3;
        style->PopupRounding = 4;
        style->ScrollbarRounding = 9;
        style->GrabRounding = 3;
        style->LogSliderDeadzone = 4;
        style->TabRounding = 4;
    }
}
