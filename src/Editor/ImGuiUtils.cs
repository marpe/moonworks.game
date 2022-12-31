using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public static unsafe class ImGuiUtils
{
    private static void EnsureTextureIsBound([NotNull] ref nint? ptr, Texture texture, ImGuiRenderer renderer)
    {
        if (texture.IsDisposed)
            throw new Exception("Attempted to bind a disposed texture");

        if (ptr != null && ptr != texture.Handle)
        {
            renderer.UnbindTexture(ptr.Value);
            ptr = null;
        }

        ptr ??= renderer.BindTexture(texture);
    }
    
    public static void DrawGame(Texture texture, Vector2 size, float scale, Vector2 offset, out Vector2 min, out Vector2 max, out Vector2 viewportSize, out Matrix4x4 viewportInvTransform)
    {
        var editor = ((MyEditorMain)Shared.Game);
        nint? textureId = null;
        EnsureTextureIsBound(ref textureId, texture, editor.ImGuiRenderer);
        
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
        
        dl->AddImage(
            (void*)textureId,
            min,
            max,
            Vector2.Zero,
            Vector2.One,
            Color.White.PackedValue
        );

        ImGui.SetCursorScreenPos(min);

        var gameRenderSize = viewportSize * scale;
        ImGui.InvisibleButton(
            "GameRender",
            gameRenderSize.EnsureNotZero(),
            ImGuiButtonFlags.MouseButtonMiddle
        );
        
        // draw border
        /*var isFocused = ImGui.IsItemFocused();
        var borderColor = (isActive, isFocused, isHovered) switch
        {
            (true, _, _) => Color.Green,
            (_, true, _) => Color.Blue,
            (_, _, true) => Color.Yellow,
            _ => Color.Gray
        };
        dl->AddRect(gameRenderMin, gameRenderMax, borderColor.PackedValue, 0, ImDrawFlags.None, 1f);*/
        
        // reset cursor position, otherwise imgui will complain since v1.89
        // where a check was added to prevent the window from being resized by just setting the cursor position 
        ImGui.SetCursorScreenPos(imGuiCursor);
        
        var viewportScale = Math.Min(
            size.X / texture.Width,
            size.Y / texture.Height
        );
        /*var viewportSize = viewportScale * new Vector2(texture.Width, texture.Height);*/
        var gameRenderOffset = min - ImGui.GetWindowViewport()->Pos;
        viewportInvTransform = (
            Matrix3x2.CreateScale(viewportScale * scale) *
            Matrix3x2.CreateTranslation(gameRenderOffset.X, gameRenderOffset.Y)
        ).ToMatrix4x4();
    }
}
