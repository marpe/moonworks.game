using Mochi.DearImGui;

namespace MyGame.Editor;

public class EditorTool
{
}

public enum RectHandlePos
{
    Top,
    Bottom,
    Left,
    Right,

    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public class SelectEditorTool : EditorTool
{
}

public enum ToolState
{
    Inactive,
    Started,
    Active,
    Ended,
}

public unsafe class ResizeEditorTool : EditorTool
{
    private static RectHandlePos[] _handlePositions = Enum.GetValues<RectHandlePos>();

    public RectHandlePos ActiveHandle = RectHandlePos.Top;

    private ToolState _state = ToolState.Inactive;
    public ToolState State => _state;

    public Point SizeDelta;
    public Point TotSizeDelta;

    private ImGuiMouseCursor[] _imGuiCursors =
    {
        ImGuiMouseCursor.ResizeNS,
        ImGuiMouseCursor.ResizeNS,
        ImGuiMouseCursor.ResizeEW,
        ImGuiMouseCursor.ResizeEW,

        ImGuiMouseCursor.ResizeNWSE,
        ImGuiMouseCursor.ResizeNESW,
        ImGuiMouseCursor.ResizeNESW,
        ImGuiMouseCursor.ResizeNWSE,
    };

    private Num.Vector2[] _pivots =
    {
        new(0, -0.5f),
        new(0, 0.5f),
        new(-0.5f, 0),
        new(0.5f, 0),

        new(-0.5f, -0.5f),
        new(0.5f, -0.5f),
        new(-0.5f, 0.5f),
        new(0.5f, 0.5f),
    };

    public ToolState Draw(Num.Vector2 min, Num.Vector2 max, float handleRadius, bool enableX, bool enableY)
    {
        if (enableX == false && enableY == false)
            return ToolState.Inactive;

        var size = max - min;
        var center = min + size * 0.5f;
        var padding = new Num.Vector2(20, 20);

        var activeHandleIndex = -1;

        for (var i = 0; i < _handlePositions.Length; i++)
        {
            var handle = _handlePositions[i];
            if (!enableX && IsXHandle(handle))
                continue;
            if (!enableY && IsYHandle(handle))
                continue;

            ImGui.PushID(i);
            var x = i % _handlePositions.Length;
            var y = i / _handlePositions.Length;

            var dl = ImGui.GetWindowDrawList();

            var pivot = _pivots[i];

            var handlePos = (center + (size + padding) * pivot);

            var wasHovered = ImGui.GetCurrentContext()->HoveredIdPreviousFrame == ImGui.GetID("ResizeHandle");

            var (fill, outline) = wasHovered switch
            {
                true => (Color.White, Color.Black),
                _ => (Color.White.MultiplyAlpha(0.33f), Color.Black.MultiplyAlpha(0.33f)),
            };

            dl->AddCircleFilled(handlePos, handleRadius, fill.PackedValue);
            dl->AddCircle(handlePos, handleRadius, outline.PackedValue);
            var handleRadiusSize = new Num.Vector2(handleRadius, handleRadius);
            ImGui.SetCursorScreenPos(handlePos - handleRadiusSize);

            ImGui.SetItemAllowOverlap();
            if (ImGui.InvisibleButton("ResizeHandle", handleRadiusSize * 2.0f))
            {
            }

            if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            {
                ImGui.SetMouseCursor(_imGuiCursors[i]);
            }

            if (ImGui.IsItemActive())
            {
                activeHandleIndex = i;
            }

            ImGui.PopID();
        }

        if (activeHandleIndex == -1)
        {
            _state = _state switch
            {
                ToolState.Ended => ToolState.Inactive,
                not ToolState.Inactive => ToolState.Ended,
                _ => ToolState.Inactive,
            };

            return _state;
        }

        ActiveHandle = _handlePositions[activeHandleIndex];

        _state = _state switch
        {
            ToolState.Started => ToolState.Active,
            not ToolState.Active => ToolState.Started,
            _ => ToolState.Active,
        };

        SizeDelta = Point.Zero;

        if (_state == ToolState.Started)
        {
            TotSizeDelta = Point.Zero;
        }

        var (invertX, invertY) = GetInvert(ActiveHandle);

        if (IsYHandle(ActiveHandle))
        {
            SizeDelta.Y = (int)ImGui.GetIO()->MouseDelta.Y * invertY;
            TotSizeDelta.Y += SizeDelta.Y;
        }

        if (IsXHandle(ActiveHandle))
        {
            SizeDelta.X = (int)ImGui.GetIO()->MouseDelta.X * invertX;
            TotSizeDelta.X += SizeDelta.X;
        }

        return _state;
    }

    private static (int invertX, int invertY) GetInvert(RectHandlePos handle)
    {
        var invertX = handle is RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft ? -1 : 1;
        var invertY = handle is RectHandlePos.TopLeft or RectHandlePos.Top or RectHandlePos.TopRight ? -1 : 1;
        return (invertX, invertY);
    }

    private static bool IsYHandle(RectHandlePos handle)
    {
        return handle is
            RectHandlePos.TopLeft or
            RectHandlePos.Top or
            RectHandlePos.TopRight or
            RectHandlePos.Bottom or
            RectHandlePos.BottomRight or
            RectHandlePos.BottomLeft;
    }

    private static bool IsXHandle(RectHandlePos handle)
    {
        return handle is
            RectHandlePos.TopLeft or
            RectHandlePos.Left or
            RectHandlePos.BottomLeft or
            RectHandlePos.Right or
            RectHandlePos.TopRight or
            RectHandlePos.BottomRight;
    }
}
