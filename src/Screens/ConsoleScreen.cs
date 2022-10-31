using MyGame.Graphics;
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

public class ConsoleScreen : IGameScreen
{
    public bool IsHidden
    {
        get => ScreenState is ScreenState.Hidden or ScreenState.TransitionOff;
        set => ScreenState = value ? ScreenState.TransitionOff : ScreenState.TransitionOn;
    }

    private KeyCode[] _pageUpAndDown = { KeyCode.PageUp, KeyCode.PageDown };
    private KeyCode[] _autoCompleteKeys = { KeyCode.Tab, KeyCode.LeftShift };
    private KeyCode[] _upAndDownArrows = { KeyCode.Up, KeyCode.Down };

    private static readonly HashSet<char> AllowedSymbols = new()
    {
        ' ', '.', ',', '\\', '/', '_', '-', '+', '=', '"', '\'',
        '!', '?', '@', '#', '$', '%', '^', '&', '*', '(', ')',
        '[', ']', '>', '<', ':', ';'
    };

    private TWConsole.TWConsole TwConsole => Shared.Console;

    private Rectangle backgroundRect;
    private readonly List<string> autoCompleteHits = new();
    private int autoCompleteIndex = -1;
    private int commandHistoryIndex = -1;
    private char[] tmpArr = new char[1];
    private int _drawCalls;
    private float _caretBlinkTimer;

    public static readonly Point CharSize = new(10, 18);

    public ScreenState ScreenState { get; private set; } = ScreenState.Hidden;

    private float TransitionPercentage;

    private InputField _inputField = new(1024, TWConsole.TWConsole.BUFFER_WIDTH);
    private readonly MyGameMain _game;
    
    [CVar("con.debug", "Show console debug info")]
    public static bool ShowDebug;

    [CVar("con.bmfont", "Use BMFont to render console")]
    public static bool UseBMFont;

    public ConsoleScreen(MyGameMain game)
    {
        _game = game;
    }

    public void Update(float deltaSeconds)
    {
        var inputState = _game.InputHandler;
        if (inputState.IsKeyPressed(KeyCode.Grave))
            IsHidden = !IsHidden;

        UpdateTransition(deltaSeconds);

        if (IsHidden)
            return;

        CheckResize();
        
        _caretBlinkTimer += deltaSeconds;

        HandleKeyPressed(inputState);

        if (inputState.MouseWheelDelta != 0)
        {
            if (Math.Sign(inputState.MouseWheelDelta) < 0)
                ScrollDown();
            else
                ScrollUp();
        }

        foreach (var c in inputState.TextInput)
        {
            HandleTextInput(c);
        }
    }

    private void CheckResize()
    {
        var windowSize = _game.MainWindow.Size;
        var availWidthInPixels = windowSize.X - ConsoleSettings.HorizontalPadding * 2f;
        var minWidth = 60;
        var width = Math.Max((int)(availWidthInPixels / CharSize.X), minWidth);
        if (TwConsole.ScreenBuffer.Width != width)
        {
            var height = (TwConsole.ScreenBuffer.Height * TwConsole.ScreenBuffer.Width) / width; // windowSize.Y / charSize.Y;
            TwConsole.ScreenBuffer.Resize(width, height);
            _inputField.SetMaxWidth(width);
            TwConsole.Print($"Console size set to: {width}, {height}");
        }
    }

    private void UpdateTransition(float deltaSeconds)
    {
        var speed = 1.0f / MathF.Clamp(ConsoleSettings.TransitionDuration, MathF.Epsilon, float.MaxValue);
        if (ScreenState == ScreenState.TransitionOn)
        {
            TransitionPercentage = MathF.Clamp01(TransitionPercentage + deltaSeconds * speed);
            if (TransitionPercentage >= 1.0f)
                ScreenState = ScreenState.Active;
        }
        else if (ScreenState == ScreenState.TransitionOff)
        {
            TransitionPercentage = MathF.Clamp01(TransitionPercentage - deltaSeconds * speed);
            if (TransitionPercentage <= 0)
                ScreenState = ScreenState.Hidden;
        }
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
                return;

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
        commandHistoryIndex = -1;
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
                commandHistoryIndex = -1;
            }
        }

        if (input.IsKeyPressed(KeyCode.Tab, true))
        {
            if (autoCompleteIndex == -1) // new auto complete
            {
                autoCompleteHits.Clear();

                if (_inputField.Length > 0)
                {
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
                var direction = input.IsKeyDown(KeyCode.LeftShift) ? -1 : 1;
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
        else if (input.IsKeyPressed(KeyCode.Down, true))
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
        autoCompleteIndex = -1;
    }

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
            return;

        var winSize = _game.MainWindow.Size;

        backgroundRect.X = 0;
        var height = (int)(winSize.Y * ConsoleSettings.RelativeConsoleHeight);
        backgroundRect.Y = (int)(height * (TransitionPercentage - 1));
        backgroundRect.Width = winSize.X;
        backgroundRect.Height = height;

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
            var color = GetColor(ConsoleSettings.ScrollIndicatorColor);

            if(UseBMFont)
                renderer.DrawBMText(ConsoleSettings.ScrollIndicatorChar, scrollIndicatorPosition, 0, color);
            else
                renderer.DrawText(ConsoleSettings.ScrollIndicatorChar, scrollIndicatorPosition, 0, color);
        }

        var displayPosition = new Vector2(
            ConsoleSettings.HorizontalPadding,
            backgroundRect.Bottom - CharSize.Y
        );

        var showInput = !hasScrolled;
        if (showInput)
            displayPosition.Y -= _inputField.Height * CharSize.Y;

        // Draw history
        var historyPosition = new Vector2(displayPosition.X, displayPosition.Y);

        var numLinesToDraw = (int)(backgroundRect.Height / CharSize.Y);

        _drawCalls = 0;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < numLinesToDraw; i++)
        {
            var lineIndex = TwConsole.ScreenBuffer.DisplayY - i;
            if (lineIndex < 0)
                break;
            var numDrawnLines = TwConsole.ScreenBuffer.CursorY - lineIndex;
            if (numDrawnLines >= TwConsole.ScreenBuffer.Height) // past scrollback wrap point
                break;

            for (var j = 0; j < TwConsole.ScreenBuffer.Width; j++)
            {
                TwConsole.ScreenBuffer.GetChar(j, lineIndex, out var c, out var color);
                if (c < 0x20 || c > 0x7e)
                    continue;
                if (c == ' ')
                    continue;
                var charColor = GetColor(color);
                var position = displayPosition + new Vector2(CharSize.X * j, -CharSize.Y * i);
                tmpArr[0] = c;
                if(UseBMFont)
                    renderer.DrawBMText(tmpArr, position, 0, charColor);
                else
                    renderer.DrawText(tmpArr, position, charColor);
                _drawCalls++;
            }
        }

        var elapsedMs = sw.ElapsedMilliseconds;
        
        if (showInput)
            DrawInput(renderer, textArea, displayPosition);


        if (ShowDebug)
        {
            var scrolledLinesStr =
                $"DrawCalls({_drawCalls}) DisplayY({TwConsole.ScreenBuffer.DisplayY}) CursorY({TwConsole.ScreenBuffer.CursorY}) Elapsed({elapsedMs}ms)";
            var lineLength = scrolledLinesStr.Length * CharSize.X;
            var scrollLinesPos = new Vector2(
                backgroundRect.Width - lineLength - ConsoleSettings.HorizontalPadding,
                0
            );

            renderer.DrawRect(new Rectangle((int)scrollLinesPos.X, 0, lineLength, CharSize.Y), Color.Black, -1f);
            if(UseBMFont)
                renderer.DrawBMText(tmpArr, scrollLinesPos, -2f, Color.Yellow * TransitionPercentage);
            else
                renderer.DrawText(scrolledLinesStr, scrollLinesPos, -2f, Color.Yellow * TransitionPercentage);
        }

        var swapTexture = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swapTexture.Width, swapTexture.Height);
        renderer.BeginRenderPass(viewProjection, false);
        // command.SetViewport(new Viewport(backgroundRect.X, backgroundRect.Y, backgroundRect.Width, backgroundRect.Height));
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
            var color = GetColor(ConsoleSettings.InputLineCharColor);
            
            if(UseBMFont)
                renderer.DrawBMText(ConsoleSettings.InputLineChar, inputLineIndicatorPosition, 0, color);
            else
                renderer.DrawText(ConsoleSettings.InputLineChar, inputLineIndicatorPosition, color);
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

                if(UseBMFont)
                    renderer.DrawBMText(tmpArr, inputPosition + new Vector2(x, y) * CharSize.ToVec2(), 0, inputColor);
                else
                    renderer.DrawText(tmpArr, inputPosition + new Vector2(x, y) * CharSize.ToVec2(), inputColor);
            }

            // Draw caret
            var caretPosition = new Vector2(
                displayPosition.X + _inputField.CursorX * CharSize.X,
                displayPosition.Y + _inputField.CursorY * CharSize.Y
            );

            var color = GetColor(ConsoleSettings.CaretColor);
            var blinkDelay = 1.5f;
            if (_caretBlinkTimer >= blinkDelay)
                color = Color.Lerp(color, Color.Transparent, MathF.Sin(ConsoleSettings.CaretBlinkSpeed * (_caretBlinkTimer - blinkDelay)));
            
            if(UseBMFont)
                renderer.DrawBMText(ConsoleSettings.CaretChar, caretPosition, 0, color);
            else
                renderer.DrawText(ConsoleSettings.CaretChar, caretPosition, color);
        }
    }
}
