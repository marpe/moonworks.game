using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public static class ImGuiBorderlessTitle
{
    public unsafe static void Draw(Window window, MyEditorMain editor)
    {
        var mainViewport = ImGui.GetMainViewport();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(0, 0));
        ImGuiInternal.BeginViewportSideBar("SideBar", mainViewport, ImGuiDir.Up, 34, ImGuiWindowFlags.NoDecoration);
        ImGui.PopStyleVar();
        var avail = ImGui.GetContentRegionAvail();
        
        ImGui.InvisibleButton("Title", avail, (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap);
        
        // move window while dragging the title bar
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var windowPos = ImGui.GetWindowPos() + ImGui.GetIO()->MouseDelta;
            SDL.SDL_SetWindowPosition(window.Handle, (int)windowPos.X, (int)windowPos.Y);
        }

        // maximize/restore when double clicking title bar
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (window.IsMaximized)
                SDL.SDL_RestoreWindow(window.Handle);
            else
                SDL.SDL_MaximizeWindow(window.Handle);
        }

        ImGui.SetItemAllowOverlap();

        ImGui.SetCursorPos(new Num.Vector2(avail.X - 29 * 3 - 6, 6));
        if (ImGuiExt.ColoredButton(FontAwesome6.WindowMinimize, Color.White * 0.5f, Color.Transparent, "Minimize")) 
        {
            window.IsMinimized = true;
        }

        ImGui.SameLine();
        var icon = window.IsMaximized ? FontAwesome6.WindowRestore : FontAwesome6.WindowMaximize;
        if (ImGuiExt.ColoredButton(icon, Color.White * 0.5f, Color.Transparent, "Maximize")) 
        {
            var isFullScreenDesktop = ((SDL.SDL_WindowFlags)SDL.SDL_GetWindowFlags(window.Handle) &
                                       SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP) == SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;
            if (window.IsMaximized || isFullScreenDesktop)
                SDL.SDL_RestoreWindow(window.Handle);
            else
                window.IsMaximized = true;
        }

        ImGui.SameLine();
        if (ImGuiExt.ColoredButton(FontAwesome6.Xmark, Color.White * 0.5f, Color.Transparent, "Close"))
        {
            editor.Quit();
        }

        ImGui.End();
    }
}
