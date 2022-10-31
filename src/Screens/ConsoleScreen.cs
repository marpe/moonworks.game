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

public class ConsoleScreen
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

    private Rectangle _backgroundRect;
    private readonly List<string> _autoCompleteHits = new();
    private int _autoCompleteIndex = -1;
    private int _commandHistoryIndex = -1;
    private char[] _tmpArr = new char[1];
    private int _charsDrawn;
    private float _caretBlinkTimer;

    public static readonly Point CharSize = new(10, 18);

    public ScreenState ScreenState { get; private set; } = ScreenState.Hidden;

    private float _transitionPercentage;

    private InputField _inputField = new(1024, TWConsole.TWConsole.BUFFER_WIDTH);
    private readonly MyGameMain _game;

    private Texture _renderTarget;

    private float _lastRenderTime;

    public ConsoleScreen(MyGameMain game)
    {
        _game = game;
        
        var windowSize = game.MainWindow.Size;
        _renderTarget = Texture.CreateTexture2D(game.GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y, TextureFormat.B8G8R8A8, TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
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
            _transitionPercentage = MathF.Clamp01(_transitionPercentage + deltaSeconds * speed);
            if (_transitionPercentage >= 1.0f)
                ScreenState = ScreenState.Active;
        }
        else if (ScreenState == ScreenState.TransitionOff)
        {
            _transitionPercentage = MathF.Clamp01(_transitionPercentage - deltaSeconds * speed);
            if (_transitionPercentage <= 0)
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
                                break;
                            if (i == _inputField.Length - 1)
                                _autoCompleteHits.Add(key);
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
        return color;
    }

    private void DrawText(Renderer renderer, ReadOnlySpan<char> text, Vector2 position, float depth, Color color)
    {
        if(ConsoleSettings.UseBMFont)
            renderer.DrawBMText(text, position, depth, color);
        else
            renderer.DrawText(text, position, depth, color);
    }
    
    public void Draw(Renderer renderer)
    {
        if (ScreenState == ScreenState.Hidden)
            return;

        var _frameTime = 1.0f / ConsoleSettings.RenderRatePerSecond;
        if (_game.TotalElapsedTime - _lastRenderTime >= _frameTime)
        {
            DrawInternal(renderer);
            _lastRenderTime = _game.TotalElapsedTime;
        }
        
        var sprite = new Sprite(_renderTarget);
        renderer.DrawSprite(sprite, Matrix3x2.Identity, Color.White * _transitionPercentage, 0);

        var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.FlushBatches(swap, viewProjection);
    }

    private void DrawInternal(Renderer renderer)
    {
        var winSize = _game.MainWindow.Size;
        TextureUtils.EnsureTextureSize(ref _renderTarget, _game.GraphicsDevice, (uint)winSize.X, (uint)winSize.Y);
        
        _backgroundRect.X = 0;
        var height = (int)(winSize.Y * ConsoleSettings.RelativeConsoleHeight);
        _backgroundRect.Y = (int)(height * (_transitionPercentage - 1));
        _backgroundRect.Width = winSize.X;
        _backgroundRect.Height = height;

        renderer.DrawRect(_backgroundRect, ConsoleSettings.BackgroundColor * ConsoleSettings.BackgroundAlpha, 0);

        // draw line start and end
        var textArea = new Rectangle(
            (int)ConsoleSettings.HorizontalPadding,
            _backgroundRect.Top,
            (int)CharSize.X * TwConsole.ScreenBuffer.Width,
            _backgroundRect.Height
        );

        var marginColor = Color.Orange * 0.5f;
        renderer.DrawLine(textArea.Min(), textArea.BottomLeft(), marginColor);
        renderer.DrawLine(textArea.TopRight(), textArea.Max(), marginColor);

        var hasScrolled = TwConsole.ScreenBuffer.DisplayY != TwConsole.ScreenBuffer.CursorY;
        if (hasScrolled)
        {
            var bottomRight = new Vector2(_backgroundRect.Width, _backgroundRect.Height);
            var scrollIndicatorPosition = bottomRight - new Vector2(CharSize.Y, ConsoleSettings.HorizontalPadding);
            var color = GetColor(ConsoleSettings.ScrollIndicatorColor);
            DrawText(renderer, ConsoleSettings.ScrollIndicatorChar, scrollIndicatorPosition, 0, color);
        }

        var displayPosition = new Vector2(
            ConsoleSettings.HorizontalPadding,
            _backgroundRect.Bottom - CharSize.Y
        );

        var showInput = !hasScrolled;
        if (showInput)
            displayPosition.Y -= _inputField.Height * CharSize.Y;

        // Draw history
        var historyPosition = new Vector2(displayPosition.X, displayPosition.Y);

        var numLinesToDraw = (int)(_backgroundRect.Height / CharSize.Y);

        _charsDrawn = 0;

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
                _tmpArr[0] = c;
                DrawText(renderer, _tmpArr, position, 0, charColor);
                _charsDrawn++;
            }
        }

        var elapsedMs = sw.ElapsedMilliseconds;
        
        if (showInput)
            DrawInput(renderer, textArea, displayPosition);


        if (ConsoleSettings.ShowDebug)
        {
            var drawCalls = ConsoleSettings.UseBMFont ? renderer.SpriteBatch.DrawCalls : renderer.TextBatcher.DrawCalls;
            var scrolledLinesStr =
                $"CharsDrawn({_charsDrawn}) DrawCalls({drawCalls}) DisplayY({TwConsole.ScreenBuffer.DisplayY}) CursorY({TwConsole.ScreenBuffer.CursorY}) Elapsed({elapsedMs}ms)";
            var lineLength = scrolledLinesStr.Length * CharSize.X;
            var scrollLinesPos = new Vector2(
                _backgroundRect.Width - lineLength - ConsoleSettings.HorizontalPadding,
                0
            );

            renderer.DrawRect(new Rectangle((int)scrollLinesPos.X, 0, lineLength, CharSize.Y), Color.Black, -1f);
            DrawText(renderer, scrolledLinesStr, scrollLinesPos, -2f, Color.Yellow);
        }
        
        // flush to render target
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, _renderTarget.Width, _renderTarget.Height);
        renderer.FlushBatches(_renderTarget, viewProjection, Color.Transparent);
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

        if (0 <= inputLineIndicatorPosition.Y && inputLineIndicatorPosition.Y <= _backgroundRect.Bottom)
        {
            var color = GetColor(ConsoleSettings.InputLineCharColor);
            DrawText(renderer, ConsoleSettings.InputLineChar, inputLineIndicatorPosition, 0, color);
        }

        if (0 <= displayPosition.Y && displayPosition.Y <= _backgroundRect.Bottom)
        {
            var inputColor = GetColor(ConsoleSettings.InputColor);
            if (_autoCompleteIndex != -1)
                inputColor = GetColor(ConsoleSettings.AutocompleteSuggestionColor);
            else if (_commandHistoryIndex != -1)
                inputColor = GetColor(ConsoleSettings.ActiveCommandHistoryColor);

            var inputPosition = displayPosition;
            for (var i = 0; i < _inputField.Length; i++)
            {
                var x = i % _inputField.MaxWidth;
                var y = i / _inputField.MaxWidth;

                _tmpArr[0] = _inputField.Buffer[i];
                DrawText(renderer, _tmpArr, inputPosition + new Vector2(x, y) * CharSize.ToVec2(), 0, inputColor);
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
            DrawText(renderer, ConsoleSettings.CaretChar, caretPosition, 0, color);
        }
    }
}
