using Mochi.DearImGui;
using MyGame.Graphics;
using MyGame.Screens;
using SDL2;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";
    private const int TARGET_TIMESTEP = 120;

    private readonly MenuScreen _menuScreen;

    public readonly InputHandler InputHandler;
    public readonly LoadingScreen LoadingScreen;

    public readonly Renderer Renderer;
    private double _drawFps;

    private ImGuiScreen? _imGuiScreen;

    private double _nextFPSUpdate = 1.0;
    private ulong _prevDrawCount;
    private ulong _prevUpdateCount;

    private readonly Stopwatch _stopwatch;
    private double _updateFps;

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
        Renderer.DefaultClearColor = Color.Black;

        GameScreen = new GameScreen(this);
        _menuScreen = new MenuScreen(this);
        ConsoleScreen = new ConsoleScreen(this);

        Task.Run(() => { _imGuiScreen = new ImGuiScreen(this); });

        Logger.LogInfo($"Game Loaded in {_stopwatch.ElapsedMilliseconds} ms");
    }

    public ulong UpdateCount { get; private set; }
    public ulong DrawCount { get; private set; }
    public float TotalElapsedTime { get; private set; }
    public float ElapsedTime { get; private set; }
    public ConsoleScreen ConsoleScreen { get; }

    public GameScreen GameScreen { get; }


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
            ConsoleScreen.Update(ElapsedTime);

            var allowKeyboardInput = ConsoleScreen.IsHidden;
            var allowMouseInput = ConsoleScreen.IsHidden;

            _menuScreen.Update(ElapsedTime, allowKeyboardInput, allowMouseInput);

            if (!_menuScreen.IsHidden)
            {
                allowKeyboardInput = false;
                allowMouseInput = false;
            }

            unsafe
            {
                if (_imGuiScreen != null)
                {
                    _imGuiScreen.Update(ElapsedTime, allowKeyboardInput, allowMouseInput);
                    if (!ImGuiScreen.IsHidden)
                    {
                        var io = ImGui.GetIO();
                        if (io->WantCaptureKeyboard)
                        {
                            allowKeyboardInput = false;
                        }

                        if (io->WantCaptureMouse)
                        {
                            allowMouseInput = false;
                        }
                    }
                }
            }

            var isPaused = !_menuScreen.IsHidden;
            GameScreen.Update(isPaused, ElapsedTime, allowKeyboardInput, allowMouseInput);
        }


        InputHandler.EndFrame();
    }


    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
        {
            return;
        }

        DrawCount++;

        if (!Renderer.BeginFrame())
        {
            return;
        }

        GameScreen.Draw(Renderer, alpha);

        _imGuiScreen?.Draw(Renderer);

        _menuScreen.Draw(Renderer, alpha);

        ConsoleScreen.Draw(Renderer, alpha);

        LoadingScreen.Draw(Renderer, alpha);

        Renderer.FlushBatches();

        Renderer.EndFrame();
    }

    protected override void Destroy()
    {
        _imGuiScreen?.Destroy();

        GameScreen.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        Shared.Console.SaveCVars();
    }
}
