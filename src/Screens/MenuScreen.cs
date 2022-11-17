using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public abstract class MenuScreen
{
    public ScreenState State = ScreenState.Hidden;
    private ScreenState _lastState = ScreenState.Hidden;
    private float _transitionPercentage;

    public bool IsHidden
    {
        get => State is ScreenState.Hidden or ScreenState.TransitionOff;
        set => State = value ? ScreenState.TransitionOff : ScreenState.TransitionOn;
    }

    private Easing.Function.Float _easeFunc = Easing.Function.Float.InOutQuad;

    public static Color BackgroundColor = Color.CornflowerBlue * 0.5f;
    public static Color HighlightColor = Color.Yellow;
    public static Color DisabledColor = Color.Black * 0.66f;
    public static Color NormalColor = Color.White;

    protected readonly List<MenuItem> _menuItems = new();
    protected readonly MenuManager _menuManager;

    protected int _selectedIndex = 0;
    public Vector2 Position = MyGameMain.DesignResolution / 2;
    private Vector2 _previousPosition;

    [CVar("menu_transition_duration", "")] public static float TransitionDuration = 1.0f;

    public MenuScreen(MenuManager menuManager)
    {
        _menuManager = menuManager;
    }

    private void UpdateTransition(float deltaSeconds, uint windowHeight)
    {
        var speed = 1.0f / MathF.Clamp(TransitionDuration, MathF.Epsilon, float.MaxValue);
        if (State == ScreenState.TransitionOn)
        {
            _transitionPercentage = MathF.Clamp01(_transitionPercentage + deltaSeconds * speed);
            if (_transitionPercentage >= 1.0f)
            {
                State = ScreenState.Active;
            }
        }
        else if (State == ScreenState.TransitionOff)
        {
            _transitionPercentage = MathF.Clamp01(_transitionPercentage - deltaSeconds * speed);
            if (_transitionPercentage <= 0)
            {
                State = ScreenState.Hidden;
            }
        }

        var height = windowHeight * 0.5f;
        var t = Easing.Function.Get(_easeFunc).Invoke(0f, 1f, _transitionPercentage, 1f);
        Position.Y = (int)(height * t);
    }


    protected void NextItem()
    {
        var startIndex = _selectedIndex;
        var numItems = _menuItems.Count;
        for (var i = 1; i < numItems; i++)
        {
            var j = (_menuItems.Count + startIndex + i) % _menuItems.Count;
            if (_menuItems[j].IsSelectable)
            {
                _selectedIndex = j;
                return;
            }
        }
    }

    protected void PreviousItem()
    {
        var startIndex = _selectedIndex;
        var numItems = _menuItems.Count;
        for (var i = 1; i < numItems; i++)
        {
            var j = (_menuItems.Count + startIndex - i) % _menuItems.Count;
            if (_menuItems[j].IsSelectable)
            {
                _selectedIndex = j;
                return;
            }
        }
    }

    public virtual void OnCancelled()
    {
    }

    public virtual void Update(float deltaSeconds)
    {
        _previousPosition = Position;

        UpdateTransition(deltaSeconds, MyGameMain.DesignResolution.Y);

        if ((State == ScreenState.TransitionOn && _lastState != ScreenState.TransitionOn))
        {
            // select first item
            _selectedIndex = 0;
            if (!_menuItems[_selectedIndex].IsSelectable)
            {
                NextItem();
            }
        }

        _lastState = State;

        if (IsHidden)
            return;

        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Down) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.S))
        {
            NextItem();
        }
        else if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Up) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.W))
        {
            PreviousItem();
        }

        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Return) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Space))
        {
            _menuItems[_selectedIndex].Callback.Invoke();
        }

        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Escape) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Backspace))
        {
            Logger.LogInfo("Cancelling screen");
            OnCancelled();
        }

        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (_menuItems[i] is FancyMenuItem ft)
            {
                ft.Update(deltaSeconds);
            }
        }

        // disable input for the next screen
        _menuManager.Game.InputHandler.MouseEnabled = _menuManager.Game.InputHandler.KeyboardEnabled = false;
    }

    public virtual void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var position = Vector2.Lerp(_previousPosition, Position, (float)alpha);
        var lineHeight = 50;

        for (var i = 0; i < _menuItems.Count; i++)
        {
            var isSelected = _selectedIndex == i;
            var isEnabled = _menuItems[i].IsEnabled;
            var isFancy = _menuItems[i] is FancyMenuItem;
            var color = (isSelected, isEnabled, isFancy) switch
            {
                (true, _, _) => HighlightColor,
                (_, false, _) => DisabledColor,
                (_, _, true) => Color.White,
                (_, _, _) => NormalColor
            };
            _menuItems[i].Draw(renderer, position, HorizontalAlignment.Center, VerticalAlignment.Top, color * _transitionPercentage);
            position.Y += lineHeight;
        }
    }
}
