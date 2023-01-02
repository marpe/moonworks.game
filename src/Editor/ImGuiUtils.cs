using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public static unsafe class ImGuiUtils
{
    public static void DrawGame(Texture texture, Vector2 size, float scale, Vector2 offset, bool usePointFiltering, out Vector2 min, out Vector2 max, out Vector2 viewportSize, out Matrix3x2 viewportInvTransform)
    {
        var editor = ((MyEditorMain)Shared.Game);
        
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(size.ToXNA().ToPoint(), texture.Size());

        var viewportPosition = new Vector2(viewport.X, viewport.Y);
        var imGuiCursor = ImGui.GetCursorScreenPos();

        viewportSize = viewport.Size.ToNumerics();
        var viewportHalfSize = viewportSize * 0.5f;

        min = imGuiCursor + viewportPosition + viewportHalfSize - // this gets us to the center
                            scale * viewportHalfSize +
                            scale * offset;
        max = min + scale * viewportSize;

        var dl = ImGui.GetWindowDrawList();
        
        editor.ImGuiRenderer.BindTexture(texture, usePointFiltering);
        dl->AddImage(
            (void*)texture.Handle,
            min,
            max,
            Vector2.Zero,
            Vector2.One,
            Color.White.PackedValue
        );

        var viewportScale = Math.Min(
            size.X / texture.Width,
            size.Y / texture.Height
        );
        /*var viewportSize = viewportScale * new Vector2(texture.Width, texture.Height);*/
        var gameRenderOffset = min - ImGui.GetWindowViewport()->Pos;
        viewportInvTransform = (
            Matrix3x2.CreateScale(viewportScale * scale) *
            Matrix3x2.CreateTranslation(gameRenderOffset.X, gameRenderOffset.Y)
        );
    }
}
