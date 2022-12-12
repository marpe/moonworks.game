namespace MyGame.Debug;

public enum FPSDisplayPosition
{
    TopLeft,
    TopRight,
    BottomRight,
    BottomLeft
}

public class FPSDisplay
{
    private Stopwatch _renderStopwatch = new();
    private Stopwatch _renderGameStopwatch = new();
    private Stopwatch _updateStopwatch = new();
    
    private float _renderDurationMs;
    private float _renderGameDurationMs;
    private float _updateDurationMs;
    
    private float _peakUpdateDurationMs;
    private float _peakRenderDurationMs;
    private float _peakRenderGameDurationMs;

    public void BeginRender()
    {
        _renderStopwatch.Restart();
    }

    public void EndRender()
    {
        _renderStopwatch.Stop();
        _renderDurationMs = _renderStopwatch.GetElapsedMilliseconds();
    }
    
    public void BeginRenderGame()
    {
        _renderGameStopwatch.Restart();
    }

    public void EndRenderGame()
    {
        _renderGameStopwatch.Stop();
        _renderGameDurationMs = _renderGameStopwatch.GetElapsedMilliseconds();
    }
    
    public void BeginUpdate()
    {
        _updateStopwatch.Restart();
    }

    public void EndUpdate()
    {
        _updateStopwatch.Stop();
        _updateDurationMs = _updateStopwatch.GetElapsedMilliseconds();
    }

    public void DrawFPS(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, FPSDisplayPosition corner = FPSDisplayPosition.BottomRight)
    {
        var origin = corner switch
        {
            FPSDisplayPosition.TopLeft => new Vector2(0, 0),
            FPSDisplayPosition.TopRight => new Vector2(1, 0),
            FPSDisplayPosition.BottomRight => new Vector2(1, 1),
            _ => new Vector2(0, 1),
        };

        var position = renderDestination.Size() * origin;
        _peakUpdateDurationMs = StopwatchExt.SmoothValue(_peakUpdateDurationMs, _updateDurationMs);
        _peakRenderDurationMs = StopwatchExt.SmoothValue(_peakRenderDurationMs, _renderDurationMs);
        _peakRenderGameDurationMs = StopwatchExt.SmoothValue(_peakRenderGameDurationMs, _renderGameDurationMs);

        var updateFps = Shared.Game.Time.UpdateFps;
        var drawFps = Shared.Game.Time.DrawFps;
        
        var str = $"Update: {updateFps:0.##} ({_peakUpdateDurationMs:00.00}), Draw: {drawFps:0.##} ({_peakRenderGameDurationMs:00.00}/{_peakRenderDurationMs:00.00})";
        var strSize = renderer.TextBatcher.GetFont(FontType.ConsolasMonoMedium).MeasureString(str);
        var min = position - strSize * origin;
        var max = min + strSize;
        var bg = RectangleExt.FromMinMax(min, max);
        
        renderer.DrawRect(bg, Color.Black * 0.66f);
        renderer.DrawText(FontType.ConsolasMonoMedium, str, min, 0, Color.Yellow);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }
}
