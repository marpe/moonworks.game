using SDL2;

namespace MyGame.Input;

public struct InputState
{
    public static readonly Vector2 DefaultMousePosition = new(-float.MaxValue, -float.MaxValue);
    public bool[] KeyboardState = new bool[232];
    public bool[] MouseState = new bool[3];
    public Vector2 GlobalMousePosition = DefaultMousePosition;
    public int MouseWheelDelta = 0;
    public char[] TextInput = Array.Empty<char>();
    public int NumTextInputChars = 0;

    public InputState()
    {
    }

    public static void Clear(ref InputState inputState)
    {
        inputState.KeyboardState.AsSpan().Fill(false);
        inputState.MouseState.AsSpan().Fill(false);
        inputState.GlobalMousePosition = DefaultMousePosition;
        inputState.MouseWheelDelta = 0;
        inputState.TextInput.AsSpan().Fill('\0');
        inputState.NumTextInputChars = 0;
    }

    public static bool IsKeyDown(in InputState inpuState, KeyCode keyCode)
    {
        return inpuState.KeyboardState[(int)keyCode];
    }

    public static bool IsAnyKeyDown(in InputState inputState, KeyCode[] keyCodes)
    {
        for (var i = 0; i < keyCodes.Length; i++)
        {
            if (inputState.KeyboardState[(int)keyCodes[i]])
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMouseButtonDown(in InputState inputState, MouseButtonCode mouseButton)
    {
        return inputState.MouseState[(int)mouseButton];
    }

    public void Print(string label)
    {
        List<KeyCode> codes = new();
        for (var i = 0; i < KeyboardState.Length; i++)
        {
            if (KeyboardState[i])
            {
                codes.Add((KeyCode)i);
            }
        }

        if (NumTextInputChars > 0 || codes.Count > 0)
        {
            Logger.LogInfo($"{label}: Text: {new string(TextInput, 0, NumTextInputChars)}, Keys: {string.Join(", ", codes.Select(x => x.ToString()))}");
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

            for (var i = 0; i < state.KeyboardState.Length; i++)
            {
                result.KeyboardState[i] |= state.KeyboardState[i];
            }

            result.GlobalMousePosition = state.GlobalMousePosition;
            result.MouseWheelDelta += state.MouseWheelDelta;
            for (var i = 0; i < 3; i++)
            {
                result.MouseState[i] |= state.MouseState[i];
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

        for (var i = 0; i < state.KeyboardState.Length; i++)
        {
            if (Enum.IsDefined((KeyCode)i))
            {
                state.KeyboardState[i] = input.IsKeyDown((KeyCode)i);
            }
        }

        SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
        state.GlobalMousePosition = new Vector2(globalMouseX, globalMouseY);
        state.MouseWheelDelta = input.MouseWheelDelta;
        for (var i = 0; i < 3; i++)
        {
            state.MouseState[i] = input.IsMouseButtonDown((MouseButtonCode)i);
        }

        return state;
    }
}
