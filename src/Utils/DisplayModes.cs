namespace MyGame.Utils;

public static class DisplayModes
{
    public static DisplayMode[] GetDisplayModes(IntPtr windowHandle)
    {
        var displayModes = new List<DisplayMode>();
        var displayIndex = SDL2.SDL.SDL_GetWindowDisplayIndex(windowHandle);
        var numDisplayModes = SDL2.SDL.SDL_GetNumDisplayModes(displayIndex);
        if (numDisplayModes >= 1)
        {
            for (var i = 0; i < numDisplayModes; i++)
            {
                var result = SDL2.SDL.SDL_GetDisplayMode(displayIndex, i, out var displayMode);
                if (result != 0)
                {
                    throw new InvalidOperationException($"Failed to get display mode: {SDL2.SDL.SDL_GetError()}");
                }

                // Display modes are sorted from largest to smallest and highest refresh rate to lowest, see https://wiki.libsdl.org/SDL_GetDisplayMode
                // Don't bother with entries that have the same size but different refresh rate
                if (i > 0 && displayModes[^1].X == displayMode.w && displayModes[^1].Y == displayMode.h)
                    continue;

                displayModes.Add(new DisplayMode(displayMode.w, displayMode.h, displayMode.refresh_rate));
            }
        }
        else
        {
            throw new InvalidOperationException($"Failed to get display modes: {SDL2.SDL.SDL_GetError()}");
        }

        return displayModes.ToArray();
    }
}
