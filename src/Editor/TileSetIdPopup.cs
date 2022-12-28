using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

using Vector2 = Num.Vector2;

public static unsafe class TileSetIdPopup
{
    public static float Scale = 4f;
    public static Vector2 Offset = Vector2.Zero;

    public static bool DrawTileSetIdPopup(string id, TileSetDef tileSetDef, out int selectedTileId)
    {
        selectedTileId = -1;
        var result = false;
        SplitWindow.GetTileSetTexture(tileSetDef.Path, out var texture);
        var mainWindowSize = ((MyEditorMain)Shared.Game).MainWindow.Size;
        var windowSize = new Vector2((int)(mainWindowSize.X * 0.66f), (int)(mainWindowSize.Y * 0.66f));
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport()->WorkPos + ImGui.GetMainViewport()->WorkSize * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowBgAlpha(1.0f);
        if (ImGui.BeginPopup(id, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.IsWindowAppearing())
            {
                Offset = Vector2.Zero;
                Scale = 4f;
            }

            var textureSize = new Vector2(texture.Width, texture.Height) * Scale;
            var cursorPos = ImGui.GetCursorScreenPos();
            var texturePosition = cursorPos + ImGui.GetWindowSize() * 0.5f - textureSize * 0.5f + Offset * Scale;
            var dl = ImGui.GetWindowDrawList();

            ImGuiExt.FillWithStripes(dl, new ImRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize()), Color.White.MultiplyAlpha(0.025f).PackedValue);
            ImGuiExt.FillWithStripes(dl, new ImRect(texturePosition, texturePosition + textureSize), Color.White.MultiplyAlpha(0.1f).PackedValue);
            
            ImGui.SetCursorScreenPos(texturePosition);
            ImGui.Image(
                (void*)texture.Handle,
                textureSize,
                Vector2.Zero,
                Vector2.One,
                Color.White.ToNumerics(),
                Color.Black.ToNumerics()
            );
            
            var textureWidth = texture.Width;
            var textureHeight = texture.Height;
            
            var cols = (int)MathF.Ceil((textureWidth - tileSetDef.Padding * 2) / (float)(tileSetDef.TileGridSize + tileSetDef.Spacing));
            var rows = (int)MathF.Ceil((textureHeight - tileSetDef.Padding * 2) / (float)(tileSetDef.TileGridSize + tileSetDef.Spacing));

            var cellSize = new Vector2(tileSetDef.TileGridSize * Scale);
            var padding = tileSetDef.Padding * Scale;
            var spacing = tileSetDef.Spacing * Scale;
            
            if (ImGui.IsItemHovered())
            {
                var mouseCellX = (int)((ImGui.GetMousePos().X - texturePosition.X - padding) / (cellSize.X + spacing));
                var mouseCellY = (int)((ImGui.GetMousePos().Y - texturePosition.Y - padding) / (cellSize.Y + spacing));
                var mouseCellPosX = texturePosition.X + padding + mouseCellX * (cellSize.X + spacing);
                var mouseCellPosY = texturePosition.Y + padding + mouseCellY * (cellSize.Y + spacing);
                var mouseCellMin = new Vector2(mouseCellPosX, mouseCellPosY);
                var mouseCellMax = mouseCellMin + cellSize;
                ImGuiExt.RectWithOutline(dl, mouseCellMin, mouseCellMax, Color.Red.MultiplyAlpha(0.1f), Color.Red, 0);

                var mouseCell = mouseCellY * cols + mouseCellX;
                var cellLabel = $"#{mouseCell}";
                var cellLabelSize = ImGui.CalcTextSize(cellLabel);
                var cellLabelPosition = texturePosition + new Vector2((textureSize.X - cellLabelSize.X) * 0.5f, -ImGui.GetTextLineHeightWithSpacing());
                dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, cellLabelPosition, Color.White.PackedValue, cellLabel, 0, default);
            }

            var paddingV = padding * Vector2.One;
            var spacingV = spacing * Vector2.One;
            
            for (var y = 0; y < rows; y++)
            {
                ImGui.PushID(y);
                for (var x = 0; x < cols; x++)
                {
                    ImGui.PushID(x);
                    var cell = new Vector2(x, y);
                    ImGui.SetCursorScreenPos(texturePosition + paddingV + cell * (cellSize + spacingV));
                    if (ImGui.InvisibleButton($"AddTileId_{x}_{y}", cellSize))
                    {
                        selectedTileId = (int)(y * cols + x);
                        result = true;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.PopID();
                }

                ImGui.PopID();
            }

            if (ImGui.IsWindowHovered())
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
                {
                    Offset += ImGui.GetIO()->MouseDelta * 1f / Scale;
                }

                if (ImGui.GetIO()->MouseWheel != 0)
                {
                    Scale += 0.1f * ImGui.GetIO()->MouseWheel * Scale;
                }
            }

            ImGui.EndPopup();
        }

        return result;
    }
}
