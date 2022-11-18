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

    [CVar("menu_transition_duration", "")] public static float TransitionDuration = 1.0f;
    private int ItemSpacingY = 20;

    public MenuScreen(MenuManager menuManager)
    {
        _menuManager = menuManager;
    }

    private void UpdateTransition(float deltaSeconds)
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
        UpdateTransition(deltaSeconds);

        if ((State == ScreenState.TransitionOn && _lastState != ScreenState.TransitionOn))
        {
            // select first item
            _selectedIndex = 0;

            if (!_menuItems[_selectedIndex].IsSelectable)
            {
                NextItem();
            }
        }

        var t = Easing.Function.Get(_easeFunc).Invoke(0f, 1f, _transitionPercentage, 1f);
        var xPos = MyGameMain.DesignResolution.X * 0.5f * t;
        var p = new Vector2(xPos, MyGameMain.DesignResolution.Y * 0.5f);

        _lastState = State;

        for (var i = 0; i < _menuItems.Count; i++)
        {
            _menuItems[i].PreviousPosition = _menuItems[i].Position;
            _menuItems[i].Position = p;

            p.Y += _menuItems[i].Height + ItemSpacingY;

            if (_menuItems[i] is FancyTextMenuItem ft)
            {
                ft.Update(deltaSeconds);
            }
        }

        if (IsHidden)
            return;

        if (_menuManager.Game.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            Logger.LogInfo("Pressed!");
        }
        
        if (_menuManager.Game.Inputs.Mouse.AnyPressed)
        {
            Logger.LogInfo("AnyPressed!");
        }

        var itemClicked = false;
        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (_menuItems[i].Bounds.Contains(_menuManager.Game.InputHandler.MousePosition) && _menuItems[i].IsSelectable)
            {
                _selectedIndex = i;

                if (_menuManager.Game.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left))
                {
                    if (_menuItems[i] is TextMenuItem tmi)
                    {
                        tmi.Callback.Invoke();
                        itemClicked = true;
                        break;
                    }

                    Logger.LogInfo("Clicked item!");
                }
            }
        }

        if (!itemClicked)
        {
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
                if (_menuItems[_selectedIndex] is TextMenuItem tmi)
                {
                    tmi.Callback.Invoke();
                }
            }

            if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Escape) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Backspace))
            {
                Logger.LogInfo("Cancelling screen");
                OnCancelled();
            }
        }

        // disable input for the next screen
        _menuManager.Game.InputHandler.MouseEnabled = _menuManager.Game.InputHandler.KeyboardEnabled = false;
    }

    public virtual void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        for (var i = 0; i < _menuItems.Count; i++)
        {
            var isSelected = _selectedIndex == i;
            var menuItem = _menuItems[i];
            var isEnabled = menuItem.IsEnabled;
            var isFancy = menuItem is FancyTextMenuItem;
            var color = (isSelected, isEnabled, isFancy) switch
            {
                (true, _, _) => HighlightColor,
                (_, false, false) => DisabledColor,
                (_, _, true) => Color.White,
                (_, _, _) => NormalColor
            };
            var p = Vector2.Lerp(menuItem.PreviousPosition, menuItem.Position, (float)alpha);
            menuItem.Draw(p, renderer, color * _transitionPercentage);
            var bounds = menuItem.Bounds;
            renderer.DrawRect(bounds.Min(), bounds.Max(), Color.Green, 2f);
        }
    }
}
