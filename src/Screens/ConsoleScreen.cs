using MyGame.Input;
using MyGame.TWConsole;
using MyGame.Utils;
using SDL2;

namespace MyGame.Screens;

public enum ScreenState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden,
}

public class ConsoleScreen
{
    private static readonly HashSet<char> AllowedSymbols = new()
    {
        ' ', '.', ',', '\\', '/', '_', '-', '+', '=', '"', '\'',
        '!', '?', '@', '#', '$', '%', '^', '&', '*', '(', ')',
        '[', ']', '>', '<', ':', ';',
    };

    public static readonly Point CharSize = new(10, 18);

    private readonly List<string> _autoCompleteHits = new();
    private readonly Color[] _colors;
    private readonly MyGameMain _game;
    private int _autoCompleteIndex = -1;
    private readonly KeyCode[] _autoCompleteKeys = { KeyCode.Tab, KeyCode.LeftShift };

    private Rectangle _backgroundRect;
    private float _caretBlinkTimer;
    private int _charsDrawn;
    private int _commandHistoryIndex = -1;
    private Easing.Function.Float _easeFunc = Easing.Function.Float.InOutQuad;
    private readonly Easing.Function.Float[] _easeFuncs = Enum.GetValues<Easing.Function.Float>();

    private readonly InputField _inputField = new(1024, TWConsole.TWConsole.BUFFER_WIDTH);

    private readonly KeyCode[] _pageUpAndDown = { KeyCode.PageUp, KeyCode.PageDown };

    private Texture _renderTarget;

    private readonly char[] _tmpArr = new char[1];

    private float _transitionPercentage;
    private readonly KeyCode[] _upAndDownArrows = { KeyCode.Up, KeyCode.Down };
    private bool _hasRender;

    public bool IsHidden
    {
        get => ScreenState is ScreenState.Hidden or ScreenState.TransitionOff;
        set => ScreenState = value ? ScreenState.TransitionOff : ScreenState.TransitionOn;
    }

    private TWConsole.TWConsole TwConsole => Shared.Console;

    public ScreenState ScreenState { get; private set; } = ScreenState.Hidden;

    public ConsoleScreen(MyGameMain game)
    {
        _game = game;

        var sz = MyGameMain.DesignResolution;
        _renderTarget = Texture.CreateTexture2D(
            game.GraphicsDevice, sz.X, sz.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget
        );

        _colors = new[]
        {
            ConsoleSettings.Color0,
            ConsoleSettings.Color1,
            ConsoleSettings.Color2,
            ConsoleSettings.Color3,
            ConsoleSettings.Color4,
            ConsoleSettings.Color5,
            ConsoleSettings.Color6,
            ConsoleSettings.Color7,
            ConsoleSettings.Color8,
            ConsoleSettings.Color9,
        };
    }

    public void Unload()
    {
        _renderTarget.Dispose();
    }

    public void Update(float deltaSeconds)
    {
        var inputState = _game.InputHandler;
        if (inputState.IsKeyPressed(KeyCode.Grave))
            IsHidden = !IsHidden;

        var sz = MyGameMain.DesignResolution;
        UpdateTransition(deltaSeconds, sz.X, sz.Y);

        if (IsHidden)
            return;

        CheckResize(sz.X, sz.Y);

        _caretBlinkTimer += deltaSeconds;

        HandleKeyPressed(inputState);

        if (inputState.MouseWheelDelta != 0)
        {
            if (Math.Sign(inputState.MouseWheelDelta) < 0)
            {
                ScrollDown();
            }
            else
            {
                ScrollUp();
            }
        }

        var textInput = inputState.GetTextInput();
        for (var i = 0; i < textInput.Length; i++)
        {
            HandleTextInput(textInput[i]);
        }

        // disable input for the next screen
        inputState.MouseEnabled = inputState.KeyboardEnabled = false;
    }

    private void CheckResize(uint windowWidth, uint windowHeight)
    {
        var availWidthInPixels = windowWidth - ConsoleSettings.HorizontalPadding * 2f;
        var minWidth = 60;
        var width = Math.Max((int)(availWidthInPixels / CharSize.X), minWidth);
        if (TwConsole.ScreenBuffer.Width != width)
        {
            var height = TwConsole.ScreenBuffer.Height * TwConsole.ScreenBuffer.Width / width; // windowSize.Y / charSize.Y;
            TwConsole.ScreenBuffer.Resize(width, height);
            _inputField.SetMaxWidth(width);
            TwConsole.Print($"Console size set to: {width}, {height}");
        }
    }

    private void UpdateTransition(float deltaSeconds, uint windowWidth, uint windowHeight)
    {
        var speed = 1.0f / MathF.Clamp(ConsoleSettings.TransitionDuration, MathF.Epsilon, float.MaxValue);
        if (ScreenState == ScreenState.TransitionOn)
        {
            _transitionPercentage = MathF.Clamp01(_transitionPercentage + deltaSeconds * speed);
            if (_transitionPercentage >= 1.0f)
            {
                ScreenState = ScreenState.Active;
            }
        }
        else if (ScreenState == ScreenState.TransitionOff)
        {
            _transitionPercentage = MathF.Clamp01(_transitionPercentage - deltaSeconds * speed);
            if (_transitionPercentage <= 0)
            {
                ScreenState = ScreenState.Hidden;
            }
        }

        var height = windowHeight * ConsoleSettings.RelativeConsoleHeight;
        var t = Easing.Function.Get(_easeFunc).Invoke(0f, 1f, _transitionPercentage, 1f);
        _backgroundRect.Y = (int)(-height + height * t);
        _backgroundRect.Height = (int)height;
        _backgroundRect.Width = (int)windowWidth;
    }

    private static bool IsAllowedCharacter(char c)
    {
        return char.IsLetter(c) ||
               char.IsNumber(c) ||
               AllowedSymbols.Contains(c);
    }

    private void HandleTextInput(char c)
    {
        if (c == InputHandler.ControlV) // CTRL + V
        {
            PasteFromClipboard();
        }
        else
        {
            if (!IsAllowedCharacter(c))
            {
                return;
            }

            _inputField.AddChar(c);
        }

        _caretBlinkTimer = 0;
    }

    private void PasteFromClipboard()
    {
        var clipboard = SDL.SDL_GetClipboardText();
        for (var i = 0; i < clipboard.Length; i++)
        {
            HandleTextInput(clipboard[i]);
        }
    }

    private void Execute()
    {
        var trimmedInput = _inputField.GetBuffer();
        TwConsole.Execute(trimmedInput);
        _commandHistoryIndex = -1;
        EndAutocomplete();
        _inputField.ClearInput();
    }

    private void HandleKeyPressed(InputHandler input)
    {
        if (input.IsAnyKeyPressed(true))
        {
            _caretBlinkTimer = 0;
        }

        if (input.IsAnyKeyPressed() && !input.IsAnyModifierKeyDown())
        {
            if (!input.IsAnyKeyDown(_autoCompleteKeys))
            {
                EndAutocomplete();
            }

            if (!input.IsAnyKeyDown(_pageUpAndDown))
            {
                TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY;
            }

            if (!input.IsAnyKeyDown(_upAndDownArrows))
            {
                _commandHistoryIndex = -1;
            }
        }

        if (input.IsKeyPressed(KeyCode.Tab, true))
        {
            if (_autoCompleteIndex == -1) // new auto complete
            {
                _autoCompleteHits.Clear();

                if (_inputField.Length > 0)
                {
                    foreach (var key in TwConsole.Commands.Keys)
                    {
                        for (var i = 0; i < _inputField.Length && i < key.Length; i++)
                        {
                            if (key[i] != _inputField.Buffer[i])
                            {
                                break;
                            }

                            if (i == _inputField.Length - 1)
                            {
                                _autoCompleteHits.Add(key);
                            }
                        }
                    }

                    TwConsole.Print($"{_autoCompleteHits.Count} matches:\n{string.Join("\n", _autoCompleteHits)}");
                }
            }

            if (_autoCompleteHits.Count == 0)
            {
                EndAutocomplete();
            }
            else if (_autoCompleteHits.Count == 1)
            {
                _inputField.SetInput(_autoCompleteHits[0]);
            }
            else
            {
                var direction = input.IsKeyDown(KeyCode.LeftShift) ? -1 : 1;
                _autoCompleteIndex += direction;

                if (_autoCompleteIndex < 0)
                {
                    _autoCompleteIndex = _autoCompleteHits.Count - 1;
                }
                else if (_autoCompleteIndex >= _autoCompleteHits.Count)
                {
                    _autoCompleteIndex = 0;
                }

                _inputField.SetInput(_autoCompleteHits[_autoCompleteIndex]);
            }
        }
        else if (input.IsKeyPressed(KeyCode.Left, true))
        {
            _inputField.CursorLeft();
        }
        else if (input.IsKeyPressed(KeyCode.Right, true))
        {
            _inputField.CursorRight();
        }
        else if (input.IsKeyPressed(KeyCode.Up, true))
        {
            _commandHistoryIndex++;
            if (_commandHistoryIndex > TwConsole.CommandHistory.Count - 1)
            {
                _commandHistoryIndex = TwConsole.CommandHistory.Count - 1;
            }

            if (_commandHistoryIndex != -1)
            {
                _inputField.SetInput(TwConsole.CommandHistory[TwConsole.CommandHistory.Count - 1 - _commandHistoryIndex]);
            }
        }
        else if (input.IsKeyPressed(KeyCode.Down, true))
        {
            _commandHistoryIndex--;
            if (_commandHistoryIndex <= -1)
            {
                _commandHistoryIndex = -1;
                _inputField.ClearInput();
            }

            if (_commandHistoryIndex != -1)
            {
                _inputField.SetInput(TwConsole.CommandHistory[TwConsole.CommandHistory.Count - 1 - _commandHistoryIndex]);
            }
        }
        else if (input.IsKeyPressed(KeyCode.PageDown, true))
        {
            if (input.IsAnyKeyDown(InputHandler.ControlKeys))
            {
                ScrollBottom();
            }
            else
            {
                ScrollDown();
            }
        }
        else if (input.IsKeyPressed(KeyCode.PageUp, true))
        {
            if (input.IsAnyKeyDown(InputHandler.ControlKeys))
            {
                ScrollTop();
            }
            else
            {
                ScrollUp();
            }
        }

        if (input.IsAnyKeyDown(InputHandler.ControlKeys))
        {
            if (input.IsKeyPressed(KeyCode.C))
            {
                _inputField.ClearInput();
            }
        }

        if (input.IsAnyKeyDown(InputHandler.ShiftKeys))
        {
            if (input.IsKeyPressed(KeyCode.Insert))
            {
                PasteFromClipboard();
            }
        }

        if (input.IsKeyPressed(KeyCode.Backspace, true))
        {
            _inputField.RemoveChar();
        }

        if (input.IsKeyPressed(KeyCode.Return, true))
        {
            Execute();
        }

        if (input.IsKeyPressed(KeyCode.Delete, true))
        {
            _inputField.Delete();
        }

        if (input.IsKeyPressed(KeyCode.End, true))
        {
            _inputField.SetCursor(_inputField.Length);
        }

        if (input.IsKeyPressed(KeyCode.Home, true))
        {
            _inputField.SetCursor(0);
        }

        if (input.IsKeyPressed(KeyCode.F12))
        {
            _easeFunc = (Easing.Function.Float)((_easeFuncs.Length + (int)_easeFunc + 1) % _easeFuncs.Length);
            TwConsole.Print($"EaseFunc: {_easeFunc}");
        }
        else if (input.IsKeyPressed(KeyCode.F11))
        {
            _easeFunc = (Easing.Function.Float)((_easeFuncs.Length + (int)_easeFunc - 1) % _easeFuncs.Length);
            TwConsole.Print($"EaseFunc: {_easeFunc}");
        }
    }

    private void ScrollTop()
    {
        TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY - TwConsole.ScreenBuffer.Height;
    }

    private void ScrollUp()
    {
        TwConsole.ScreenBuffer.DisplayY -= ConsoleSettings.ScrollSpeed;
    }

    private void ScrollDown()
    {
        TwConsole.ScreenBuffer.DisplayY += ConsoleSettings.ScrollSpeed;
    }

    private void ScrollBottom()
    {
        TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY;
    }

    private void EndAutocomplete()
    {
        _autoCompleteIndex = -1;
    }

    private Color GetColor(int colorIndex)
    {
        return _colors[colorIndex];
    }

    private void DrawText(Renderer renderer, ReadOnlySpan<char> text, Vector2 position, float depth, Color color)
    {
        if (ConsoleSettings.UseBMFont)
        {
            renderer.DrawBMText(BMFontType.ConsolasMonoSmall, text, position, Vector2.Zero, Vector2.One, 0, depth, color);
        }
        else
        {
            renderer.DrawText(FontType.ConsolasMonoMedium, text, position, depth, color);
        }
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (ScreenState == ScreenState.Hidden)
            return;

        if ((int)_game.Time.DrawCount % ConsoleSettings.RenderRate == 0)
        {
            _hasRender = true;
            DrawInternal(renderer, alpha);
            renderer.End(commandBuffer, _renderTarget, Color.Transparent, null);
        }

        if (!_hasRender)
            return;

        var sprite = new Sprite(_renderTarget);
        renderer.DrawSprite(sprite, Matrix4x4.Identity, Color.White * _transitionPercentage, 0);
        renderer.End(commandBuffer, renderDestination, null, null);
    }

    private void DrawInternal(Renderer renderer, double alpha)
    {
        renderer.DrawRect(_backgroundRect, ConsoleSettings.BackgroundColor * ConsoleSettings.BackgroundAlpha);

        // draw line start and end
        var textArea = new Rectangle(
            ConsoleSettings.HorizontalPadding,
            _backgroundRect.Top,
            CharSize.X * TwConsole.ScreenBuffer.Width,
            _backgroundRect.Height
        );

        var marginColor = Color.Orange * 0.5f;
        renderer.DrawLine(textArea.Min(), textArea.BottomLeft(), marginColor, 1f);
        renderer.DrawLine(textArea.TopRight(), textArea.Max(), marginColor, 1f);

        var hasScrolled = TwConsole.ScreenBuffer.DisplayY != TwConsole.ScreenBuffer.CursorY;
        if (hasScrolled)
        {
            var bottomRight = new Vector2(_backgroundRect.Width, _backgroundRect.Height);
            var scrollIndicatorPosition = bottomRight - new Vector2(CharSize.Y, ConsoleSettings.HorizontalPadding);
            var color = ConsoleSettings.ScrollIndicatorColor;
            DrawText(renderer, ConsoleSettings.ScrollIndicatorChar, scrollIndicatorPosition, 0, color);
        }

        var displayPosition = new Vector2(
            ConsoleSettings.HorizontalPadding,
            _backgroundRect.Bottom - CharSize.Y
        );

        var showInput = !hasScrolled;
        if (showInput)
        {
            displayPosition.Y -= _inputField.Height * CharSize.Y;
        }

        // Draw history
        var historyPosition = new Vector2(displayPosition.X, displayPosition.Y);

        var numLinesToDraw = _backgroundRect.Height / CharSize.Y;

        _charsDrawn = 0;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < numLinesToDraw; i++)
        {
            var lineIndex = TwConsole.ScreenBuffer.DisplayY - i;
            if (lineIndex < 0)
            {
                break;
            }

            var numDrawnLines = TwConsole.ScreenBuffer.CursorY - lineIndex;
            if (numDrawnLines >= TwConsole.ScreenBuffer.Height) // past scrollback wrap point
            {
                break;
            }

            for (var j = 0; j < TwConsole.ScreenBuffer.Width; j++)
            {
                TwConsole.ScreenBuffer.GetChar(j, lineIndex, out var c, out var color);
                if (c < 0x20 || c > 0x7e)
                {
                    continue;
                }

                if (c == ' ')
                {
                    continue;
                }

                var charColor = GetColor(color);
                var position = displayPosition + new Vector2(CharSize.X * j, -CharSize.Y * i);
                _tmpArr[0] = c;
                DrawText(renderer, _tmpArr, position, 0, charColor);
                _charsDrawn++;
            }
        }


        if (showInput)
        {
            DrawInput(renderer, textArea, displayPosition);
        }

        if (ConsoleSettings.ShowDebug)
        {
            var scrolledLinesStr =
                $"CharsDrawn({_charsDrawn}) " +
                $"DrawCalls({renderer.SpriteBatch.DrawCalls}) " +
                $"DisplayY({TwConsole.ScreenBuffer.DisplayY}) " +
                $"CursorY({TwConsole.ScreenBuffer.CursorY}) " +
                $"Elapsed({sw.ElapsedMilliseconds}ms) ";
            var lineLength = scrolledLinesStr.Length * CharSize.X;
            var scrollLinesPos = new Vector2(
                _backgroundRect.Width - lineLength - ConsoleSettings.HorizontalPadding,
                0
            );

            renderer.DrawRect(new Rectangle((int)scrollLinesPos.X, 0, lineLength, CharSize.Y), Color.Black, -1f);
            DrawText(renderer, scrolledLinesStr, scrollLinesPos, -2f, Color.Yellow);
        }
    }

    private void DrawInput(Renderer renderer, Rectangle textArea, Vector2 displayPosition)
    {
        // draw input background
        renderer.DrawRect(
            new Rectangle(
                textArea.Left,
                (int)displayPosition.Y,
                textArea.Width,
                CharSize.Y + _inputField.Height * CharSize.Y
            ),
            ConsoleSettings.InputBackgroundColor
        );

        // Draw input line indicator
        var inputLineIndicatorPosition = displayPosition - Vector2.UnitX * CharSize.X;

        if (0 <= inputLineIndicatorPosition.Y && inputLineIndicatorPosition.Y <= _backgroundRect.Bottom)
        {
            DrawText(renderer, ConsoleSettings.InputLineChar, inputLineIndicatorPosition, 0, ConsoleSettings.InputLineCharColor);
        }

        if (0 <= displayPosition.Y && displayPosition.Y <= _backgroundRect.Bottom)
        {
            var inputColor = ConsoleSettings.InputTextColor;
            if (_autoCompleteIndex != -1)
            {
                inputColor = ConsoleSettings.AutocompleteSuggestionColor;
            }
            else if (_commandHistoryIndex != -1)
            {
                inputColor = ConsoleSettings.ActiveCommandHistoryColor;
            }

            var inputPosition = displayPosition;
            for (var i = 0; i < _inputField.Length; i++)
            {
                var x = i % _inputField.MaxWidth;
                var y = i / _inputField.MaxWidth;

                _tmpArr[0] = _inputField.Buffer[i];
                DrawText(renderer, _tmpArr, inputPosition + new Vector2(x, y) * CharSize, 0, inputColor);
            }

            // Draw caret
            var caretPosition = new Vector2(
                displayPosition.X + _inputField.CursorX * CharSize.X,
                displayPosition.Y + _inputField.CursorY * CharSize.Y
            );

            var color = ConsoleSettings.CaretColor;
            var blinkDelay = 1.5f;
            if (_caretBlinkTimer >= blinkDelay)
            {
                color = Color.Lerp(color, Color.Transparent, MathF.Sin(ConsoleSettings.CaretBlinkSpeed * (_caretBlinkTimer - blinkDelay)));
            }

            DrawText(renderer, ConsoleSettings.CaretChar, caretPosition, 0, color);
        }
    }
}
