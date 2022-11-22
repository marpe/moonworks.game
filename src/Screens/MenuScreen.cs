namespace MyGame.Screens;

public enum MenuScreenState
{
    Active,
    Covered,
    Exiting,
    Exited
}

public abstract class MenuScreen
{
    [CVar("menu.debug", "Toggle menu debugging")]
    public static bool Debug;

    public MenuScreenState State { get; private set; } = MenuScreenState.Exited;
    private float _transitionPercentage;

    private Easing.Function.Float _easeFunc = Easing.Function.Float.InOutQuad;

    public static Color BackgroundColor = Color.CornflowerBlue * 0.5f;
    public static Color HighlightColor = Color.Yellow;
    public static Color DisabledColor = Color.Black * 0.66f;
    public static Color NormalColor = Color.White;

    protected readonly List<MenuItem> _menuItems = new();
    protected int _selectedIndex = 0;

    [CVar("menu_transition_duration", "")] public static float TransitionDuration = 0.25f;
    private int ItemSpacingY = 20;

    protected MenuScreen? _child;
    protected MyGameMain _game;

    public MenuScreen(MyGameMain game)
    {
        _game = game;
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

    public void SetChild(MenuScreen? menu)
    {
        var prevMenu = _child;
        _child = menu;

        if (_child != null)
        {
            _child.SetState(MenuScreenState.Active);
            SetState(MenuScreenState.Covered);

            if (_child != prevMenu)
                _child.OnBecameVisible();
        }
        else
        {
            SetState(MenuScreenState.Active);
        }
    }

    public virtual void OnCancelled()
    {
    }

    public void Exit()
    {
        SetState(MenuScreenState.Exiting);
    }

    public void SetState(MenuScreenState newState)
    {
        State = newState;
    }

    public virtual void OnBecameVisible()
    {
        _transitionPercentage = 0;
        _child = null;

        // select first item
        _selectedIndex = 0;

        if (!_menuItems[_selectedIndex].IsSelectable)
        {
            NextItem();
        }
    }

    public virtual void Update(float deltaSeconds)
    {
        UpdateTransition(deltaSeconds);
        UpdateMenuItems(deltaSeconds);

        if (State == MenuScreenState.Active)
        {
            HandleInput();
        }

        if (_child != null)
        {
            _child.Update(deltaSeconds);
            if (_child.State == MenuScreenState.Exiting)
                SetState(MenuScreenState.Active);
            else if (_child.State == MenuScreenState.Exited)
                SetChild(null);
        }
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

    private void UpdateTransition(float deltaSeconds)
    {
        var transitionDirection = State == MenuScreenState.Active ? 1 : -1;
        var speed = 1.0f / MathF.Clamp(TransitionDuration, MathF.Epsilon, float.MaxValue);
        _transitionPercentage = MathF.Clamp01(_transitionPercentage + transitionDirection * deltaSeconds * speed);

        if (_transitionPercentage == 0 && State == MenuScreenState.Exiting)
            SetState(MenuScreenState.Exited);
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

            _selectedIndex = i;

            if (!_game.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left))
                continue;

            if (_menuItems[i] is TextMenuItem tmi)
                tmi.Callback.Invoke();

            return true;
        }

        return false;
    }

    private void UpdateMenuItems(float deltaSeconds)
    {
        var t = Easing.Function.Get(_easeFunc).Invoke(0f, 1f, _transitionPercentage, 1f);
        var xPos = MyGameMain.DesignResolution.X * 0.5f * t;
        var p = new Vector2(xPos, MyGameMain.DesignResolution.Y * 0.5f);

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
                (true, _, _) => HighlightColor,
                (_, false, false) => DisabledColor,
                (_, _, true) => Color.White,
                (_, _, _) => NormalColor
            };
            var p = Vector2.Lerp(menuItem.PreviousPosition, menuItem.Position, (float)alpha);

            if (isFancy)
                color.A = (byte)(color.A * _transitionPercentage);
            else
                color *= _transitionPercentage;

            menuItem.Draw(p, renderer, color);
            var bounds = menuItem.Bounds;

            if (Debug)
                renderer.DrawRect(bounds.Min(), bounds.Max(), Color.Green, 2f);
        }

        if (_child != null)
            _child.Draw(renderer, alpha);
    }
}
