namespace MyGame.TWConsole;

public static class ConsoleSettings
{
    public static int HistoryColor = 0;
    public static int InputColor = 0;
    public static Color BackgroundColor = Color.Black;
    public static int CaretColor = 2;
    public static int InputLineCharColor = 8;
    public static int ActiveCommandHistoryColor = 6;
    public static int AutocompleteSuggestionColor = 7;
    public static int ScrollIndicatorColor = 8;
    public static int TimestampColor = 0;

    public static Color Color0 = Color.White;
    public static Color Color1 = Color.Yellow;
    public static Color Color2 = Color.Cyan;
    public static Color Color3 = Color.DarkCyan;
    public static Color Color4 = Color.OrangeRed;
    public static Color Color5 = Color.Brown;
    public static Color Color6 = Color.Magenta;
    public static Color Color7 = Color.LightGreen;
    public static Color Color8 = Color.Yellow;
    public static Color Color9 = Color.Orange;

    [CVar("con.alpha", "Sets console alpha [0-1]")]
    public static float BackgroundAlpha = .9f;

    public static float CaretBlinkSpeed = 5f;
    public static string CaretChar = "_";
    public static string InputLineChar = "> ";
    public static int HorizontalPadding = 30;

    [CVar("con.height", "Sets console height [0-1]")]
    public static float RelativeConsoleHeight = 1f;

    public static int ScrollSpeed = 3;

    public static string ScrollIndicatorChar = "^";

    [CVar("con.speed", "Config transition duration in seconds")]
    public static float TransitionDuration = 0.1f;
    
    [CVar("con.debug", "Show console debug info")]
    public static bool ShowDebug;

    [CVar("con.bmfont", "Use BMFont to render console")]
    public static bool UseBMFont;
    
    [CVar("con.render_rate", "Controls the console render rate (fps)")]
    public static float RenderRatePerSecond = 120f;
}
