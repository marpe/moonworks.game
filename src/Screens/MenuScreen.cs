namespace MyGame.Screens;

public enum MenuScreenState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden
}

public abstract class MenuScreen
{
    public static Color BackgroundColor = Color.CornflowerBlue * 0.5f;
    public static Color HighlightColor1 = Color.Yellow;
    public static Color HighlightColor2 = Color.Red;
    public static Color DisabledColor = Color.Black * 0.66f;
    public static Color NormalColor = Color.White;

    [CVar("menu.debug", "Toggle menu debugging")]
    public static bool Debug;

    public MenuScreenState State { get; private set; } = MenuScreenState.Hidden;
    private MenuScreenState _previousState = MenuScreenState.Hidden;

    protected float _transitionPercentage;
    public static float TransitionDuration = 0.25f;

    protected readonly List<MenuItem> _menuItems = new();
    protected int _selectedIndex = 0;

    private int ItemSpacingY = 20;

    protected MyGameMain _game;
    protected Spring _spring;

    private float XOffset = 500f;
    private Vector2 InitialPosition = MyGameMain.DesignResolution.ToVec2() * 0.5f;
    private bool _wasCoveredByOtherScreen;
    private int _lastSelectedIndex;

    public MenuScreen(MyGameMain game)
    {
        _game = game;
        _spring = new Spring();

        _spring.EquilibriumPosition = -1;
        _spring.Position = -1;
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

    protected void Exit()
    {
        SetState(MenuScreenState.TransitionOff);
    }

    private void SetState(MenuScreenState newState)
    {
        _previousState = State;
        State = newState;
    }

    public virtual void OnScreenAdded()
    {
        SetState(MenuScreenState.TransitionOn);

        ResetItems();

        _transitionPercentage = 0;
        // select first item
        _selectedIndex = 0;

        if (!_menuItems[_selectedIndex].IsSelectable)
        {
            NextItem();
        }
    }

    private void ResetItems()
    {
        _spring.Position = _spring.Position < 0 ? -1 : 1;
        var position = new Vector2(InitialPosition.X + _spring.Position * XOffset, InitialPosition.Y);
        for (var i = 0; i < _menuItems.Count; i++)
        {
            _menuItems[i].PreviousPosition = _menuItems[i].Position = position;
            _menuItems[i].NudgeSpring.Position = 0;
            _menuItems[i].Alpha = 0;
        }
    }

    public virtual void Update(float deltaSeconds, bool isCoveredByOtherScreen)
    {
        UpdateTransition(deltaSeconds, isCoveredByOtherScreen);

        UpdateMenuItems(deltaSeconds, isCoveredByOtherScreen);

        if (State == MenuScreenState.TransitionOn)
        {
            if (!isCoveredByOtherScreen)
                HandleInput();

            if (_transitionPercentage >= 1)
            {
                SetState(MenuScreenState.Active);
            }
        }
        else if (State == MenuScreenState.Active)
        {
            if (!isCoveredByOtherScreen)
                HandleInput();
        }
        else if (State == MenuScreenState.TransitionOff)
        {
            if (_transitionPercentage == 0)
            {
                Shared.Menus.RemoveScreen(this);
                SetState(MenuScreenState.Hidden);
            }
        }
        else if (State == MenuScreenState.Hidden)
        {
            // noop
        }

        _wasCoveredByOtherScreen = isCoveredByOtherScreen;
    }

    private void HandleInput()
    {
        var inputHandled = false;
        if (!inputHandled)
            inputHandled = HandleMouse();
        if (!inputHandled)
            inputHandled = HandleKeyboard();
        // disable input for the next screen
        _game.InputHandler.MouseEnabled = _game.InputHandler.KeyboardEnabled = false;
    }

    private void UpdateTransition(float deltaSeconds, bool isCoveredByOtherScreen)
    {
        var isActive = State is MenuScreenState.Active or MenuScreenState.TransitionOn;
        var transitionDirection = isActive ? 1 : -1;
        var speed = 1.0f / MathF.Clamp(TransitionDuration, MathF.Epsilon, float.MaxValue);
        _transitionPercentage = MathF.Clamp01(_transitionPercentage + transitionDirection * deltaSeconds * speed);
    }

    private bool HandleKeyboard()
    {
        if (_game.InputHandler.IsKeyPressed(KeyCode.Down) || _game.InputHandler.IsKeyPressed(KeyCode.S))
        {
            NextItem();
            return true;
        }

        if (_game.InputHandler.IsKeyPressed(KeyCode.Up) || _game.InputHandler.IsKeyPressed(KeyCode.W))
        {
            PreviousItem();
            return true;
        }

        if (_game.InputHandler.IsKeyPressed(KeyCode.Return) || _game.InputHandler.IsKeyPressed(KeyCode.Space))
        {
            if (_menuItems[_selectedIndex] is TextMenuItem tmi)
                tmi.Callback.Invoke();
            return true;
        }

        if (_game.InputHandler.IsKeyPressed(KeyCode.Escape) || _game.InputHandler.IsKeyPressed(KeyCode.Backspace))
        {
            OnCancelled();
            return true;
        }

        return false;
    }

    private bool HandleMouse()
    {
        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (!_menuItems[i].Bounds.Contains(_game.InputHandler.MousePosition) || !_menuItems[i].IsSelectable)
                continue;

            if (_game.InputHandler.MouseDelta.X != 0 || _game.InputHandler.MouseDelta.Y != 0)
                _selectedIndex = i;

            if (_selectedIndex != i)
                continue;

            if (!_game.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left))
                continue;

            if (_menuItems[i] is TextMenuItem tmi)
                tmi.Callback.Invoke();

            return true;
        }

        return false;
    }

    private void UpdateMenuItems(float deltaSeconds, bool isCoveredByOtherScreen)
    {
        var targetPosition = State switch
        {
            (MenuScreenState.Active or MenuScreenState.TransitionOn) => isCoveredByOtherScreen ? -1 : 0,
            _ => 1,
        };

        _spring.EquilibriumPosition = targetPosition;
        _spring.Update(deltaSeconds);

        // var t = Easing.Function.Get(_easeFunc).Invoke(0f, 1f, _transitionPercentage, 1f);
        var position = new Vector2(InitialPosition.X + _spring.Position * XOffset, InitialPosition.Y);

        for (var i = 0; i < _menuItems.Count; i++)
        {
            _menuItems[i].PreviousPosition = _menuItems[i].Position;
            _menuItems[i].NudgeSpring.Update(deltaSeconds);
            _menuItems[i].Position = position;
            position.Y = _menuItems[i].Bounds.Bottom + ItemSpacingY;
            _menuItems[i].Position.X += _menuItems[i].NudgeSpring.Position;

            _menuItems[i].Alpha = (1.0f - MathF.Abs(_spring.Position));


            if (_menuItems[i] is FancyTextMenuItem ft)
            {
                ft.Update(deltaSeconds);
            }
        }

        if (_lastSelectedIndex != _selectedIndex)
        {
            _menuItems[_selectedIndex].NudgeSpring.Velocity = 500;
        }

        _lastSelectedIndex = _selectedIndex;
    }

    public virtual void Draw(Renderer renderer, double alpha)
    {
        for (var i = 0; i < _menuItems.Count; i++)
        {
            var isSelected = _selectedIndex == i;
            var menuItem = _menuItems[i];
            var isEnabled = menuItem.IsEnabled;
            var isFancy = menuItem is FancyTextMenuItem;
            var color = (isSelected, isEnabled, isFancy) switch
            {
                (true, _, _) => ColorExt.PulseColor(HighlightColor1, HighlightColor2, Shared.Game.Time.TotalElapsedTime),
                (_, false, false) => DisabledColor,
                (_, _, true) => Color.White,
                (_, _, _) => NormalColor
            };
            var p = Vector2.Lerp(menuItem.PreviousPosition, menuItem.Position, (float)alpha);

            menuItem.Draw(p, renderer, color);

            if (Debug)
            {
                var bounds = menuItem.Bounds;
                renderer.DrawRect(bounds.Min(), bounds.Max(), Color.Green, 2f);
            }
        }
    }
}
