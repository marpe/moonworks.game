namespace MyGame.Input;

public struct InputState
{
    private static KeyCode[] AllKeyCodes = Enum.GetValues<KeyCode>();
    private static MouseButtonCode[] AllMouseButtonCodes = Enum.GetValues<MouseButtonCode>();
    
    public static readonly Vector2 DefaultMousePosition = new(-float.MaxValue, -float.MaxValue);
    public HashSet<KeyCode> KeyboardState = new();
    public HashSet<MouseButtonCode> MouseState = new();
    public Vector2 GlobalMousePosition = DefaultMousePosition;
    public int MouseWheelDelta = 0;
    public char[] TextInput = Array.Empty<char>();
    public int NumTextInputChars = 0;

    public InputState()
    {
    }

    public static void Clear(ref InputState inputState)
    {
        inputState.KeyboardState.Clear();
        inputState.MouseState.Clear();
        inputState.GlobalMousePosition = DefaultMousePosition;
        inputState.MouseWheelDelta = 0;
        inputState.TextInput.AsSpan().Fill('\0');
        inputState.NumTextInputChars = 0;
    }

    public static bool IsKeyDown(in InputState inpuState, KeyCode keyCode)
    {
        return inpuState.KeyboardState.Contains(keyCode);
    }

    public static bool IsAnyKeyDown(in InputState inputState, KeyCode[] keyCodes)
    {
        foreach (var key in keyCodes)
        {
            if (inputState.KeyboardState.Contains(key))
                return true;
        }

        return false;
    }

    public static bool IsMouseButtonDown(in InputState inputState, MouseButtonCode mouseButton)
    {
        return inputState.MouseState.Contains(mouseButton);
    }

    public void Print(string label)
    {
        if (NumTextInputChars > 0 || KeyboardState.Count > 0)
        {
            Logger.LogInfo($"{label}: Text: {new string(TextInput, 0, NumTextInputChars)}, Keys: {string.Join(", ", KeyboardState.Select(x => x.ToString()))}");
        }
    }

    public static InputState Aggregate(List<InputState> inputStates)
    {
        var result = new InputState();

        foreach (var state in inputStates)
        {
            var oldNumChars = result.NumTextInputChars;
            result.NumTextInputChars += state.NumTextInputChars;
            if (result.TextInput.Length < result.NumTextInputChars)
            {
                Array.Resize(ref result.TextInput, result.NumTextInputChars);
            }

            for (var i = 0; i < state.NumTextInputChars; i++)
            {
                result.TextInput[oldNumChars + i] = state.TextInput[i];
            }

            foreach (var keyDown in state.KeyboardState)
            {
                result.KeyboardState.Add(keyDown);
            }

            result.GlobalMousePosition = state.GlobalMousePosition;
            result.MouseWheelDelta += state.MouseWheelDelta;

            foreach (var mouseDown in state.MouseState)
            {
                result.MouseState.Add(mouseDown);
            }
        }

        return result;
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

        for (var i = 0; i < AllKeyCodes.Length; i++)
        {
            var keyCode = AllKeyCodes[i];
            if (input.IsKeyDown(keyCode))
                state.KeyboardState.Add(keyCode);
        }

        SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
        state.GlobalMousePosition = new Vector2(globalMouseX, globalMouseY);
        state.MouseWheelDelta = input.MouseWheelDelta;
        for (var i = 0; i < AllMouseButtonCodes.Length; i++)
        {
            var mouseButton = AllMouseButtonCodes[i];
            if (input.IsMouseButtonDown(mouseButton))
                state.MouseState.Add(mouseButton);
        }

        return state;
    }
}
