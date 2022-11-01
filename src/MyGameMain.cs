using ImGuiNET;
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
    public ConsoleScreen ConsoleScreen => _consoleScreen;

    private readonly GameScreen _gameScreen;
    private readonly MenuScreen _menuScreen;

    public readonly Renderer Renderer;

    public readonly InputHandler InputHandler;

    private Stopwatch _stopwatch;
    public readonly LoadingScreen LoadingScreen;

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
        Task.Run(() => { Shared.Console.Initialize(); });

        LoadingScreen = new LoadingScreen(this);
        InputHandler = new InputHandler(this);

        Renderer = new Renderer(this);

        _gameScreen = new GameScreen(this);
        _menuScreen = new MenuScreen(this);
        _consoleScreen = new ConsoleScreen(this);

        Task.Run(() => { _imGuiScreen = new ImGuiScreen(this); });

        Logger.LogInfo($"Game Loaded in {_stopwatch.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        UpdateCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        if (_stopwatch.Elapsed.TotalSeconds >= _nextFPSUpdate)
        {
            _updateFps = UpdateCount - _prevUpdateCount;
            _drawFps = DrawCount - _prevDrawCount;
            _prevUpdateCount = UpdateCount;
            _prevDrawCount = DrawCount;
            _nextFPSUpdate = _stopwatch.Elapsed.TotalSeconds + 1.0;
            SDL.SDL_SetWindowTitle(MainWindow.Handle, $"Update: {_updateFps:0.##}, Draw: {_drawFps:0.##}");
        }

        InputHandler.BeginFrame();

        LoadingScreen.Update(ElapsedTime);

        var doUpdate = LoadingScreen.State != TransitionState.TransitionOn &&
                        LoadingScreen.State != TransitionState.Active;

        if (doUpdate)
        {
            _consoleScreen.Update(ElapsedTime);

            var allowKeyboardInput = _consoleScreen.IsHidden;
            var allowMouseInput = _consoleScreen.IsHidden;

            _menuScreen.Update(ElapsedTime, allowKeyboardInput, allowMouseInput);

            if (!_menuScreen.IsHidden)
            {
                allowKeyboardInput = false;
                allowMouseInput = false;
            }
            
            if (_imGuiScreen != null)
            {
                _imGuiScreen.Update(ElapsedTime, allowKeyboardInput, allowMouseInput);
                if (!_imGuiScreen.IsHidden)
                {
                    var io = ImGui.GetIO();
                    if (io.WantCaptureKeyboard)
                        allowKeyboardInput = false;
                    if (io.WantCaptureMouse)
                        allowMouseInput = false;
                }
            }

            _gameScreen.Update(ElapsedTime, allowKeyboardInput, allowMouseInput);
        }


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
        
        _menuScreen.Draw(Renderer);

        _consoleScreen.Draw(Renderer);

        LoadingScreen.Draw(Renderer);

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
