namespace MyGame.Utils;

public static class StringExt
{
    public static string TruncateNumber(float f)
    {
        return $"{f:f7}".Substring(0, 4);
    }
}
