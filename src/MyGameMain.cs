using MyGame.Graphics;
using MyGame.TWConsole;
using MyGame.TWImGui;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    public ulong UpdateCount { get; private set; }
    public ulong DrawCount { get; private set; }
    public float TotalElapsedTime { get; private set; }
    public float ElapsedTime { get; private set; }

    private ImGuiScreen? _imGuiScreen;
    private readonly ConsoleScreen _consoleScreen;
    private readonly GameScreen _gameScreen;

    public readonly Renderer Renderer;

    public readonly InputHandler InputHandler;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        Task.Run(() =>
        {
            Shared.Console.Initialize();
        });

        InputHandler = new InputHandler(this);
        
        Renderer = new Renderer(this);

        _gameScreen = new GameScreen(this);
        _consoleScreen = new ConsoleScreen(this);

        Task.Run(() =>
        {
            _imGuiScreen = new ImGuiScreen(this);
        });

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        UpdateCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        InputHandler.BeginFrame();
        
        _imGuiScreen?.Update(ElapsedTime);

        _consoleScreen.Update(ElapsedTime);
        
        _gameScreen.Update(ElapsedTime);

        InputHandler.EndFrame();
    }


    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        DrawCount++;

        if (!Renderer.BeginFrame())
            return;

        _gameScreen.Draw(Renderer);

        _imGuiScreen?.Draw(Renderer);

        _consoleScreen.Draw(Renderer);

        Renderer.EndFrame();
    }

    protected override void Destroy()
    {
        _imGuiScreen?.Destroy();
        Shared.Console.SaveCVars();
    }
}
