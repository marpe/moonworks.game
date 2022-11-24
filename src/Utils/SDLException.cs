namespace MyGame.Utils;

public class SDLException : Exception
{
    public SDLException(string sdlMethodName) : base($"{sdlMethodName}: {SDL.SDL_GetError()}")
    {
    }
    
    public SDLException(string message, string sdlMethodName) : base($"{message} ({sdlMethodName}: {SDL.SDL_GetError()})")
    {
    }
}
