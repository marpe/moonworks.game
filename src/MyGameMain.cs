using MyGame.Graphics;
using MyGame.Screens;
using SDL2;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";
    private const int TARGET_TIMESTEP = 120;

    public ulong UpdateCount { get; private set; }
    public ulong DrawCount { get; private set; }
    public float TotalElapsedTime { get; private set; }
    public float ElapsedTime { get; private set; }

    private ImGuiScreen? _imGuiScreen;
    private readonly ConsoleScreen _consoleScreen;
    private readonly GameScreen _gameScreen;

    public readonly Renderer Renderer;

    public readonly InputHandler InputHandler;

    private Stopwatch _stopwatch;

    private double _nextFPSUpdate = 1.0;
    private ulong _prevDrawCount;
    private ulong _prevUpdateCount;
    private double _updateFps;
    private double _drawFps;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 120, debugMode)
    {
        _stopwatch = Stopwatch.StartNew();

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

        Logger.LogInfo($"Game Loaded in {_stopwatch.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        UpdateCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        if (_stopwatch.Elapsed.TotalSeconds > _nextFPSUpdate)
        {
            _updateFps = UpdateCount - _prevUpdateCount;
            _drawFps = DrawCount - _prevDrawCount;
            _prevUpdateCount = UpdateCount;
            _prevDrawCount = DrawCount;
            _nextFPSUpdate = _stopwatch.Elapsed.TotalSeconds + 1.0;
            SDL.SDL_SetWindowTitle(MainWindow.Handle, $"Update: {_updateFps:0.##}, Draw: {_drawFps:0.##}");
        }
 
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

        _gameScreen.Unload();
        
        _consoleScreen.Unload();

        Renderer.Unload();
        
        Shared.Console.SaveCVars();
    }
}
