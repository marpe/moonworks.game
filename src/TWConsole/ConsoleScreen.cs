using MyGame.Graphics;
using MyGame.Utils;
using SDL2;

namespace MyGame.TWConsole;

public enum ScreenState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden,
}

public class ConsoleScreen
{
    public bool IsHidden
    {
        get => ScreenState is ScreenState.Hidden or ScreenState.TransitionOff;
        set => ScreenState = value ? ScreenState.TransitionOff : ScreenState.TransitionOn;
    }

    private static readonly HashSet<char> AllowedSymbols = new()
    {
        ' ', '.', ',', '\\', '/', '_', '-', '+', '=', '"', '\'',
        '!', '?', '@', '#', '$', '%', '^', '&', '*', '(', ')',
        '[', ']', '>', '<', ':', ';'
    };

    private static readonly char[] TextInputChars = new[]
    {
        (char)2, // Home
        (char)3, // End
        (char)8, // Backspace
        (char)9, // Tab
        (char)13, // Enter
        (char)127, // Delete
        (char)22 // Ctrl+V (Paste)
    };

    private TWConsole TwConsole => Shared.Console;

    private Rectangle backgroundRect;
    private readonly List<string> autoCompleteHits = new();
    private int autoCompleteIndex = -1;
    private int commandHistoryIndex = -1;
    private int LineHeight => (int)CharSize.Y;
    private Point CharSize = new(10, 18);

    public ScreenState ScreenState { get; private set; } = ScreenState.Hidden;

    private float _transitionTimer = 0;

    private float TransitionPercentage => MathF.Clamp01(_transitionTimer / ConsoleSettings.TransitionDuration);

    private InputField _inputField = new(1024, TWConsole.DEFAULT_WIDTH);
    private readonly MyGameMain _game;

    public ConsoleScreen(MyGameMain game)
    {
        _game = game;
        Inputs.TextInput += HandleTextInput;
    }

    public void Update(float deltaSeconds)
    {
        var inputState = _game.Inputs;
        if (inputState.Keyboard.IsPressed(KeyCode.I))
            IsHidden = !IsHidden;

        UpdateTransition(deltaSeconds);

        if (IsHidden)
            return;

        HandleKeyPressed(inputState);

        if (inputState.Mouse.Wheel != 0)
        {
            if (Math.Sign(inputState.Mouse.Wheel) < 0)
                ScrollDown();
            else
                ScrollUp();
        }

        /*for (var i = 0; i < inputState.ExtTextInputChars.Count; i++)
        {
            HandleTextInput(inputState.ExtTextInputChars[i]);
        }*/
    }

    private void UpdateTransition(float deltaSeconds)
    {
        if (ScreenState == ScreenState.TransitionOn)
        {
            _transitionTimer += deltaSeconds;

            if (_transitionTimer >= ConsoleSettings.TransitionDuration)
            {
                _transitionTimer = ConsoleSettings.TransitionDuration;
                ScreenState = ScreenState.Active;
            }
        }
        else if (ScreenState == ScreenState.TransitionOff)
        {
            _transitionTimer -= deltaSeconds;

            if (_transitionTimer <= 0)
            {
                _transitionTimer = 0;
                ScreenState = ScreenState.Hidden;
            }
        }
    }

    private static bool IsAllowedSymbol(char c)
    {
        return AllowedSymbols.Contains(c);
    }

    /// <summary>
    /// https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext
    /// </summary>
    /// <param name="c"></param>
    private void HandleTextInput(char c)
    {
        if (c == TextInputChars[0]) // Home
        {
            _inputField.SetCursor(0);
        }
        else
        {
            if (c == TextInputChars[1]) // End
            {
                _inputField.SetCursor(_inputField.Length);
            }
            else if (c == TextInputChars[2]) // Backspace
            {
                _inputField.RemoveChar();
            }
            else if (c == TextInputChars[3]) // Tab
            {
            }
            else if (c == TextInputChars[4]) // Enter
            {
                Execute();
            }
            else if (c == TextInputChars[5]) // Delete
            {
                _inputField.Delete();
            }
            else if (c == TextInputChars[6]) // CTRL + V
            {
                PasteFromClipboard();
            }
            else
            {
                var allowedChars = new Predicate<char>[]
                {
                    char.IsLetter,
                    char.IsNumber,
                    IsAllowedSymbol
                };

                bool isAllowed = allowedChars.Any(predicate => predicate(c));
                if (!isAllowed)
                {
                    return;
                }

                _inputField.AddChar(c);
            }
        }
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
        commandHistoryIndex = -1;
        EndAutocomplete();
        _inputField.ClearInput();
    }

    private void HandleKeyPressed(Inputs inputState)
    {
        var keyboard = inputState.Keyboard;
        var anyKeyPressed = keyboard.AnyPressed;

        var modifierKeyDown = keyboard.IsDown(KeyCode.LeftShift) ||
                              keyboard.IsDown(KeyCode.RightShift) ||
                              keyboard.IsDown(KeyCode.LeftControl) ||
                              keyboard.IsDown(KeyCode.RightControl) ||
                              keyboard.IsDown(KeyCode.LeftAlt) ||
                              keyboard.IsDown(KeyCode.RightAlt);

        if (anyKeyPressed && !modifierKeyDown)
        {
            ReadOnlySpan<KeyCode> autocompleteKeys = stackalloc KeyCode[] { KeyCode.Tab, KeyCode.LeftShift };
            if (!keyboard.IsAnyKeyDown(autocompleteKeys))
            {
                EndAutocomplete();
            }

            ReadOnlySpan<KeyCode> pgUpDown = stackalloc KeyCode[] { KeyCode.PageUp, KeyCode.PageDown };
            if (!keyboard.IsAnyKeyDown(pgUpDown))
            {
                TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY;
            }

            ReadOnlySpan<KeyCode> upDown = stackalloc KeyCode[] { KeyCode.Up, KeyCode.Down };
            if (!keyboard.IsAnyKeyDown(upDown))
            {
                commandHistoryIndex = -1;
            }
        }

        if (keyboard.IsPressed(KeyCode.Tab)) // TODO (marpe): Allow repeeating
        {
            if (autoCompleteIndex == -1) // new auto complete
            {
                autoCompleteHits.Clear();

                foreach (var key in TwConsole.Commands.Keys)
                {
                    for (var i = 0; i < _inputField.Length && i < key.Length; i++)
                    {
                        if (key[i] != _inputField.Buffer[i])
                            break;
                        if (i == _inputField.Length - 1)
                            autoCompleteHits.Add(key);
                    }
                }

                TwConsole.Print($"{autoCompleteHits.Count} matches:\n{string.Join("\n", autoCompleteHits)}");
            }

            if (autoCompleteHits.Count == 0)
            {
                EndAutocomplete();
            }
            else if (autoCompleteHits.Count == 1)
            {
                _inputField.SetInput(autoCompleteHits[0]);
            }
            else
            {
                var direction = keyboard.IsDown(KeyCode.LeftShift) ? -1 : 1;
                autoCompleteIndex += direction;

                if (autoCompleteIndex < 0)
                {
                    autoCompleteIndex = autoCompleteHits.Count - 1;
                }
                else if (autoCompleteIndex >= autoCompleteHits.Count)
                {
                    autoCompleteIndex = 0;
                }

                _inputField.SetInput(autoCompleteHits[autoCompleteIndex]);
            }
        }
        else if (keyboard.IsPressed(KeyCode.Left)) // TODO (marpe): Allow repeating
        {
            _inputField.CursorLeft();
        }
        else if (keyboard.IsPressed(KeyCode.Right)) // TODO (marpe): Allow repeating
        {
            _inputField.CursorRight();
        }
        else if (keyboard.IsPressed(KeyCode.Up)) // TODO (marpe): Allow repeating
        {
            commandHistoryIndex++;
            if (commandHistoryIndex > TwConsole.CommandHistory.Count - 1)
            {
                commandHistoryIndex = TwConsole.CommandHistory.Count - 1;
            }

            if (commandHistoryIndex != -1)
            {
                _inputField.SetInput(TwConsole.CommandHistory[TwConsole.CommandHistory.Count - 1 - commandHistoryIndex]);
            }
        }
        else if (keyboard.IsPressed(KeyCode.Down)) // TODO (marpe): Allow repeating
        {
            commandHistoryIndex--;
            if (commandHistoryIndex <= -1)
            {
                commandHistoryIndex = -1;
                _inputField.ClearInput();
            }

            if (commandHistoryIndex != -1)
            {
                _inputField.SetInput(TwConsole.CommandHistory[TwConsole.CommandHistory.Count - 1 - commandHistoryIndex]);
            }
        }
        else if (keyboard.IsPressed(KeyCode.PageDown))
        {
            if (keyboard.IsDown(KeyCode.LeftControl) ||
                keyboard.IsDown(KeyCode.RightControl))
            {
                ScrollBottom();
            }
            else
            {
                ScrollDown();
            }
        }
        else if (keyboard.IsPressed(KeyCode.PageUp))
        {
            if (keyboard.IsDown(KeyCode.LeftControl) ||
                keyboard.IsDown(KeyCode.RightControl))
            {
                ScrollTop();
            }
            else
            {
                ScrollUp();
            }
        }

        if (keyboard.IsDown(KeyCode.LeftControl) ||
            keyboard.IsDown(KeyCode.RightControl))
        {
            if (keyboard.IsPressed(KeyCode.C))
            {
                _inputField.ClearInput();
            }
        }

        ReadOnlySpan<KeyCode> shiftKeys = stackalloc KeyCode[] { KeyCode.LeftShift, KeyCode.RightShift };
        if (keyboard.IsAnyKeyDown(shiftKeys))
        {
            if (keyboard.IsPressed(KeyCode.Insert))
            {
                PasteFromClipboard();
            }
        }
    }

    private void ScrollTop()
    {
        TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.Height;
        if (TwConsole.ScreenBuffer.CursorY - TwConsole.ScreenBuffer.DisplayY >= TwConsole.ScreenBuffer.Height)
            TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY - TwConsole.ScreenBuffer.Height + 1;
    }

    private void ScrollBottom()
    {
        TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY;
    }

    private void ScrollUp()
    {
        TwConsole.ScreenBuffer.DisplayY -= ConsoleSettings.ScrollSpeed;

        if (TwConsole.ScreenBuffer.CursorY - TwConsole.ScreenBuffer.DisplayY >= TwConsole.ScreenBuffer.Height)
            TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY - TwConsole.ScreenBuffer.Height + 1;
    }

    private void ScrollDown()
    {
        TwConsole.ScreenBuffer.DisplayY += ConsoleSettings.ScrollSpeed;

        if (TwConsole.ScreenBuffer.DisplayY > TwConsole.ScreenBuffer.CursorY)
            TwConsole.ScreenBuffer.DisplayY = TwConsole.ScreenBuffer.CursorY;
    }

    private void EndAutocomplete()
    {
        autoCompleteIndex = -1;
    }

    private char[] tmpArr = new char[1];

    public static Color GetColor(int colorIndex, float alpha)
    {
        var color = colorIndex switch
        {
            0 => ConsoleSettings.Color0,
            1 => ConsoleSettings.Color1,
            2 => ConsoleSettings.Color2,
            3 => ConsoleSettings.Color3,
            4 => ConsoleSettings.Color4,
            5 => ConsoleSettings.Color5,
            6 => ConsoleSettings.Color6,
            7 => ConsoleSettings.Color7,
            8 => ConsoleSettings.Color8,
            9 => ConsoleSettings.Color9,
            _ => Color.White
        };

        return color * alpha;
    }

    private Color GetColor(int colorIndex) => GetColor(colorIndex, TransitionPercentage);

    public void Draw(Renderer renderer)
    {
        if (ScreenState == ScreenState.Hidden)
        {
            return;
        }

        var winSize = Shared.MainWindow.Size;

        backgroundRect.X = 0;
        backgroundRect.Y = (int)(winSize.Y * (TransitionPercentage - 1));
        backgroundRect.Width = winSize.X;
        backgroundRect.Height = (int)(winSize.Y * ConsoleSettings.RelativeConsoleHeight);

        var (command, swap) = (
            renderer.CommandBuffer ?? throw new InvalidOperationException(),
            renderer.Swap ?? throw new InvalidOperationException()
        );

        // var previousViewport = GameCore.GraphicsDevice.Viewport;
        // GameCore.GraphicsDevice.Viewport = new Viewport(backgroundRect);
        // command.SetViewport(new Viewport(backgroundRect.X, backgroundRect.Y, backgroundRect.Width, backgroundRect.Height));

        renderer.DrawRect(backgroundRect, ConsoleSettings.BackgroundColor * ConsoleSettings.BackgroundAlpha, 0);

        // draw line start and end
        var textArea = new Rectangle(
            (int)ConsoleSettings.HorizontalPadding,
            backgroundRect.Top,
            (int)CharSize.X * TwConsole.ScreenBuffer.Width,
            backgroundRect.Height
        );

        var marginColor = Color.Orange * 0.5f;
        renderer.DrawLine(textArea.Min(), textArea.BottomLeft(), marginColor);
        renderer.DrawLine(textArea.TopRight(), textArea.Max(), marginColor);

        var hasScrolled = TwConsole.ScreenBuffer.DisplayY != TwConsole.ScreenBuffer.CursorY;
        if (hasScrolled)
        {
            var bottomRight = new Vector2(backgroundRect.Width, backgroundRect.Height);
            var scrollIndicatorPosition = bottomRight - new Vector2(CharSize.Y, ConsoleSettings.HorizontalPadding);

            renderer.DrawText(
                ConsoleSettings.ScrollIndicatorChar,
                scrollIndicatorPosition,
                0,
                GetColor(ConsoleSettings.ScrollIndicatorColor)
            );
        }

        var scrolledLinesStr = $"DisplaY({TwConsole.ScreenBuffer.DisplayY}) CursorY({TwConsole.ScreenBuffer.CursorY})";
        var scrollLinesPos =
            new Vector2(backgroundRect.Width - scrolledLinesStr.Length * CharSize.X - ConsoleSettings.HorizontalPadding,
                0);

        renderer.DrawText(scrolledLinesStr, scrollLinesPos, Color.Yellow);

        var displayPosition = new Vector2(
            ConsoleSettings.HorizontalPadding,
            backgroundRect.Bottom - LineHeight
        );

        var showInput = !hasScrolled;
        if (showInput)
            displayPosition.Y -= _inputField.Height * CharSize.Y;

        // Draw history
        var historyPosition = new Vector2(displayPosition.X, displayPosition.Y);

        var numLinesToDraw = (int)(backgroundRect.Height / CharSize.Y);

        for (var i = 0; i < numLinesToDraw; i++)
        {
            var lineIndex = TwConsole.ScreenBuffer.DisplayY - i;
            if (lineIndex < 0)
                break;
            if (TwConsole.ScreenBuffer.CursorY - lineIndex >= TwConsole.ScreenBuffer.Height)
                break;

            for (var j = 0; j < TwConsole.ScreenBuffer.Width; j++)
            {
                var (c, color) = TwConsole.ScreenBuffer.GetChar(j, lineIndex);
                if (c == ' ')
                    continue;
                else if (c == '\0')
                    break;
                var charColor = GetColor(color);
                var position = displayPosition + new Vector2(CharSize.X * j, -CharSize.Y * i);
                // var offset = Font.DrawText(batcher, c, position, charColor);
                tmpArr[0] = c;
                renderer.DrawText(
                    tmpArr,
                    position,
                    charColor
                );
            }
        }

        if (showInput)
            DrawInput(renderer, textArea, displayPosition);

        // GameCore.GraphicsDevice.Viewport = previousViewport;

        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.BeginRenderPass(viewProjection, false);
        renderer.EndRenderPass();
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
            Color.White * 0.1f
        );

        // Draw input line indicator
        var inputLineIndicatorPosition = displayPosition - Vector2.UnitX * CharSize.X;

        if (0 <= inputLineIndicatorPosition.Y && inputLineIndicatorPosition.Y <= backgroundRect.Bottom)
        {
            renderer.DrawText(
                ConsoleSettings.InputLineChar,
                inputLineIndicatorPosition,
                GetColor(ConsoleSettings.InputLineCharColor)
            );
        }

        if (0 <= displayPosition.Y && displayPosition.Y <= backgroundRect.Bottom)
        {
            var inputColor = GetColor(ConsoleSettings.InputColor);
            if (autoCompleteIndex != -1)
                inputColor = GetColor(ConsoleSettings.AutocompleteSuggestionColor);
            else if (commandHistoryIndex != -1)
                inputColor = GetColor(ConsoleSettings.ActiveCommandHistoryColor);

            var inputPosition = displayPosition;
            for (var i = 0; i < _inputField.Length; i++)
            {
                var x = i % _inputField.MaxWidth;
                var y = i / _inputField.MaxWidth;

                tmpArr[0] = _inputField.Buffer[i];
                renderer.DrawText(
                    tmpArr,
                    inputPosition + new Vector2(x, y) * CharSize.ToVec2(),
                    inputColor
                );
            }

            // Draw caret
            var caretPosition = new Vector2(
                displayPosition.X + _inputField.CursorX * CharSize.X,
                displayPosition.Y + _inputField.CursorY * CharSize.Y
            );

            var color = GetColor(ConsoleSettings.CaretColor);
            var caretColor = Color.Lerp(color, Color.Transparent, MathF.Sin(ConsoleSettings.CaretBlinkSpeed * _game.TotalElapsedTime));
            renderer.DrawText(
                ConsoleSettings.CaretChar,
                caretPosition,
                caretColor
            );
        }
    }
}

public static class PointExt
{
    public static Vector2 ToVec2(this Point p)
    {
        return new Vector2(p.X, p.Y);
    }
}

public static class KeyboardExt
{
    public static bool IsAnyKeyDown(this Keyboard keyboard, ReadOnlySpan<KeyCode> codes)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            if (keyboard.IsDown(codes[i]))
                return true;
        }

        return false;
    }
}
