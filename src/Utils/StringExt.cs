namespace MyGame.Utils;

public static class StringExt
{
    public static string TruncateNumber(float f, int length = 4)
    {
        return $"{f:f7}".Substring(0, length);
    }
}
