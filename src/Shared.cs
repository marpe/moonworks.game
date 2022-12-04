using FreeTypeSharp;
using MyGame.Audio;

namespace MyGame;

public static class Shared
{
    public static MyGameMain Game = null!;
    public static TWConsole.TWConsole Console = null!;
    public static LoadingScreen LoadingScreen = null!;
    public static MenuHandler Menus = null!;
    public static FreeTypeLibrary FreeTypeLibrary = null!;
    public static AudioManager AudioManager = null!;
    public static ContentManager Content = null!;
}
