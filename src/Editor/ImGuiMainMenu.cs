using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe static class ImGuiMainMenu
{
    public const int MAIN_MENU_PADDING_Y = 6;
    private static void DrawMenu(ImGuiMenu menu, int depth = 0)
    {
        if (menu.Children.Count > 0)
        {
            if (depth == 0)
            {
                var style = ImGui.GetStyle();
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, MAIN_MENU_PADDING_Y));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
            }

            var result = ImGui.BeginMenu(menu.Text, menu.IsEnabled ?? true);
            if (depth == 0)
                ImGui.PopStyleVar(2);

            if (result)
            {
                foreach (var child in menu.Children)
                    DrawMenu(child, depth + 1);

                ImGui.EndMenu();
            }
        }
        else
        {
            if (ImGui.MenuItem(menu.Text, menu.Shortcut, menu.IsSelectedCallback?.Invoke() ?? false))
                menu.Callback?.Invoke();
        }
    }

    private static void CheckMenuShortcuts(ImGuiMenu menu)
    {
        if (!(menu.IsEnabled ?? true))
            return;

        if (ImGuiExt.IsKeyboardShortcutPressed(menu.Shortcut))
            menu.Callback?.Invoke();

        foreach (var child in menu.Children)
            CheckMenuShortcuts(child);
    }

    public static void DrawMenu(List<ImGuiMenu> menus, SortedList<string, ImGuiEditorWindow> windows)
    {
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, MAIN_MENU_PADDING_Y));
        var result = ImGui.BeginMainMenuBar();
        ImGui.PopStyleVar();
        if (!result)
        {
            return;
        }

        for (var i = 0; i < menus.Count - 1; i++)
        {
            DrawMenu(menus[i]);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, MAIN_MENU_PADDING_Y));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
        var windowMenu = ImGui.BeginMenu("Window");
        ImGui.PopStyleVar(2);
        if (windowMenu)
        {
            foreach (var (_, window) in windows)
            {
                ImGui.MenuItem(window.Title, window.KeyboardShortcut, ImGuiExt.RefPtr(ref window.IsOpen));
            }

            ImGui.EndMenu();
        }

        DrawMenu(menus[^1]);

        DrawMainMenuButtons();

        foreach (var menu in menus)
        {
            CheckMenuShortcuts(menu);
        }

        ImGui.EndMainMenuBar();
    }

    private static void DrawMainMenuButtons()
    {
        var max = ImGui.GetContentRegionMax();
        var numButtons = 4;
        var buttonWidth = 29;
        ImGui.SetCursorPosX((max.X - numButtons * buttonWidth) / 2);
        ImGui.SetCursorPosY(ImGui.GetStyle()->FramePadding.Y);

        ImGui.BeginChild("MainMenuButtons", new Vector2(0, 40));
        var (icon, color, tooltip) = MyGameMain.IsPaused switch
        {
            true => (FontAwesome6.Play, Color.Green, "Play"),
            _ => (FontAwesome6.Pause, Color.Yellow, "Pause")
        };

        ImGui.BeginDisabled(Shared.LoadingScreen.IsLoading);
        if (ImGuiExt.ColoredButton(LoadingIndicator(FontAwesome6.ArrowsRotate, Shared.LoadingScreen.IsLoading), Color.Blue, "Reload World"))
        {
            MyGameMain.Restart();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGuiExt.ColoredButton(icon, color, tooltip))
        {
            MyGameMain.IsPaused = !MyGameMain.IsPaused;
        }

        ImGui.SameLine();
        if (ImGuiExt.ColoredButton(FontAwesome6.ForwardStep, Color.Orange, "Step"))
        {
            MyGameMain.IsPaused = MyGameMain.IsStepping = true;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!Shared.Game.World.IsLoaded);
        if (ImGuiExt.ColoredButton(FontAwesome6.Stop, Color.Red, "Stop"))
        {
            Shared.Game.UnloadWorld();
        }
        ImGui.EndDisabled();
        
        ImGui.EndChild();
    }
    
    private static string LoadingIndicator(string labelWhenNotLoading, bool isLoading)
    {
        if (!isLoading)
            return labelWhenNotLoading;
        var n = (int)(Shared.Game.Time.TotalElapsedTime * 4 % 4);
        return n switch
        {
            0 => FontAwesome6.ArrowRight,
            1 => FontAwesome6.ArrowDown,
            2 => FontAwesome6.ArrowLeft,
            _ => FontAwesome6.ArrowUp,
        };
    }
}
