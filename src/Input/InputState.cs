namespace MyGame.Input;

public struct InputState
{
    private static KeyCode[] _allKeyCodes = Enum.GetValues<KeyCode>();
    private static MouseButtonCode[] _allMouseButtonCodes = Enum.GetValues<MouseButtonCode>();

    public static readonly Vector2 DefaultMousePosition = new(-float.MaxValue, -float.MaxValue);
    public HashSet<KeyCode> KeysDown = new();
    public HashSet<MouseButtonCode> MouseButtonsDown = new();
    public Vector2 GlobalMousePosition = DefaultMousePosition;
    public int MouseWheelDelta = 0;
    public char[] TextInput = Array.Empty<char>();
    public int NumTextInputChars = 0;

    public InputState()
    {
    }

    public static void Clear(ref InputState inputState)
    {
        inputState.KeysDown.Clear();
        inputState.MouseButtonsDown.Clear();
        inputState.GlobalMousePosition = DefaultMousePosition;
        inputState.MouseWheelDelta = 0;
        inputState.TextInput.AsSpan().Fill('\0');
        inputState.NumTextInputChars = 0;
    }

    public static bool IsKeyDown(in InputState inputState, KeyCode keyCode)
    {
        return inputState.KeysDown.Contains(keyCode);
    }
    
    public static bool IsAnyKeyDown(in InputState inputState, KeyCode[] keyCodes)
    {
        foreach (var key in keyCodes)
        {
            if (inputState.KeysDown.Contains(key))
                return true;
        }

        return false;
    }

    public static bool IsMouseButtonDown(in InputState inputState, MouseButtonCode mouseButton)
    {
        return inputState.MouseButtonsDown.Contains(mouseButton);
    }
    
    public void Print(string label)
    {
        if (NumTextInputChars > 0 || KeysDown.Count > 0)
        {
            Logs.LogInfo($"{label}: Text: {new string(TextInput, 0, NumTextInputChars)}, Keys: {string.Join(", ", KeysDown.Select(x => x.ToString()))}");
        }
    }
    
    public static InputState Create(InputHandler input)
    {
        var state = new InputState();

        var textInput = input.GetTextInput();
        Array.Resize(ref state.TextInput, textInput.Length);
        state.NumTextInputChars = textInput.Length;
        for (var i = 0; i < state.NumTextInputChars; i++)
        {
            state.TextInput[i] = textInput[i];
        }

        for (var i = 0; i < _allKeyCodes.Length; i++)
        {
            var keyCode = _allKeyCodes[i];
            if (input.IsKeyDown(keyCode))
                state.KeysDown.Add(keyCode);
        }

        SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
        state.GlobalMousePosition = new Vector2(globalMouseX, globalMouseY);
        state.MouseWheelDelta = input.MouseWheelDelta;
        for (var i = 0; i < _allMouseButtonCodes.Length; i++)
        {
            var mouseButton = _allMouseButtonCodes[i];
            if (input.IsMouseButtonDown(mouseButton))
                state.MouseButtonsDown.Add(mouseButton);
            
        }

        return state;
    }
}
