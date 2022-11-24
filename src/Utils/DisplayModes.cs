namespace MyGame.Utils;

public static class DisplayModes
{
    public static SDL.SDL_DisplayMode[] GetDisplayModes(IntPtr windowHandle)
    {
        var displayModes = new List<SDL.SDL_DisplayMode>();
        var displayIndex = SDL.SDL_GetWindowDisplayIndex(windowHandle);
        var numDisplayModes = SDL.SDL_GetNumDisplayModes(displayIndex);
        if (numDisplayModes >= 1)
        {
            for (var i = 0; i < numDisplayModes; i++)
            {
                var result = SDL.SDL_GetDisplayMode(displayIndex, i, out var displayMode);
                if (result != 0)
                {
                    throw new SDLException("Failed to get display mode", nameof(SDL.SDL_GetDisplayMode));
                }

                // Display modes are sorted from largest to smallest and highest refresh rate to lowest, see https://wiki.libsdl.org/SDL_GetDisplayMode
                // Don't bother with entries that have the same size but different refresh rate
                if (i > 0 && displayModes[^1].w == displayMode.w && displayModes[^1].w == displayMode.h)
                    continue;

                displayModes.Add(displayMode);
            }
        }
        else
        {
            throw new SDLException("Failed to get display modes", nameof(SDL.SDL_GetNumDisplayModes));
        }

        return displayModes.ToArray();
    }
}
