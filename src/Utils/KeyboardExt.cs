namespace MyGame.Utils;

public static class KeyboardExt
{
    public static bool IsAnyKeyDown(this Keyboard keyboard, ReadOnlySpan<KeyCode> codes)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            if (keyboard.IsDown(codes[i]))
            {
                return true;
            }
        }

        return false;
    }
}
