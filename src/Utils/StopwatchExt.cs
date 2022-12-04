namespace MyGame.Utils;

public static class StopwatchExt
{
    public static float GetElapsedMilliseconds(this Stopwatch stopwatch)
    {
        return (float)((double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000.0);
    }

    public static void StopAndLog(this Stopwatch stopwatch, string message)
    {
        stopwatch.Stop();
        Logs.LogVerbose($"{message}: {stopwatch.GetElapsedMilliseconds()}");
    }
    
    public static float SmoothValue(float prev, float current)
    {
        return prev > current ? MathF.Lerp(prev, current, 0.05f) : current;
    }
}
