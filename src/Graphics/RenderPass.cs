using MyGame.Cameras;
using MyGame.Debug;
using MyGame.Entities;

namespace MyGame.Graphics;

public class RenderPass
{
    public virtual void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
    }
}

public class ConsoleRenderPass : RenderPass
{
    private bool _hasRenderedConsole;
    private float _nextRender;

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (Shared.Game.Time.TotalElapsedTime >= _nextRender)
        {
            _hasRenderedConsole = true;
            renderer.Clear(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, Color.Transparent);
            renderer.DrawRect(new Vector2(0, 0), new Vector2(1, 1), Color.Black);

            ConsoleToast.Draw(renderer, Shared.Game.RenderTargets.ConsoleRender);

            if (Shared.Game.ConsoleScreen.ConsoleScreenState != ConsoleScreenState.Hidden)
            {
                Shared.Game.ConsoleScreen.Draw(renderer, alpha);
            }

            Shared.Game._fpsDisplay.DrawFPS(renderer, Shared.Game.RenderTargets.ConsoleRender.Size);
            renderer.RunRenderPass(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, null, null);

            _nextRender = Shared.Game.Time.TotalElapsedTime + 1.0f / ConsoleSettings.RenderFPS;
        }

        if (_hasRenderedConsole)
        {
            renderer.DrawSprite(Shared.Game.RenderTargets.ConsoleRender, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
        }
    }
}

public class WorldRenderPass : RenderPass
{
    private List<Light> _lights = new();
    private TextureSamplerBinding[] _lightTextureSamplerBindings = new TextureSamplerBinding[1];
    private TextureSamplerBinding[] _rimLightTextureSamplerBindings = new TextureSamplerBinding[2];

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var renderTargets = Shared.Game.RenderTargets;
        DrawWorld(renderer, ref commandBuffer, renderTargets.GameRender, alpha);

        var camera = Shared.Game.Camera;
        // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
        camera.GetViewFloored(alpha, out var floorRemainder);
        TestFunctions.DrawPixelArtShaderTestSkull(renderer, ref commandBuffer, renderTargets.GameRender,
            renderTargets.GameRender.Size / (2 * 4) + floorRemainder * renderTargets.RenderScale);
        var srcPos = floorRemainder * renderTargets.RenderScale;
        var srcSize = renderTargets.GameRender.Size - UPoint.One * (uint)renderTargets.RenderScale;
        var srcRect = new Bounds(srcPos.X, srcPos.Y, srcSize.X, srcSize.Y);
        var dstSize = renderDestination.Size();
        var dstRect = new Bounds(0, 0, dstSize.X, dstSize.Y);

        renderer.DrawSprite(renderTargets.GameRender, srcRect, dstRect, Color.White);

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, false);
    }

    private void DrawWorld(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        renderer.Clear(ref commandBuffer, renderDestination, Color.Black);

        var world = Shared.Game.World;
        if (!world.IsLoaded)
        {
            return;
        }

        var camera = Shared.Game.Camera;
        var renderTargets = Shared.Game.RenderTargets;

        var view = camera.GetViewFloored(alpha, out _) *
                   Matrix3x2.CreateScale(renderTargets.RenderScale);
        var projection = Renderer.GetOrthographicProjection(renderTargets.GameRender.Width, renderTargets.GameRender.Height);
        var viewProjection = view.ToMatrix4x4() * projection;

        LevelRenderer.DrawLevel(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LevelBase, Color.Transparent, viewProjection);
        
        world.DrawEntities(renderer, alpha);
        var entitiesViewProjection = view.ToMatrix4x4() *
                                     projection;
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LevelBase, null, entitiesViewProjection, false, PipelineType.PixelArt);

        LevelRenderer.DrawBackground(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        world.DrawDebug(renderer, camera, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.Background, Color.Transparent, viewProjection);

        renderer.DrawSprite(renderTargets.Background, Matrix4x4.Identity, Color.White);
        renderer.DrawSprite(renderTargets.LevelBase, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);

        renderer.DrawSprite(renderTargets.LevelBase, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LightBase, Color.Transparent, null);

        if (World.LightsEnabled)
        {
            renderer.Clear(ref commandBuffer, renderTargets.NormalLights, world.AmbientColor);

            DrawAllLights(
                renderer,
                renderTargets.LightBase,
                renderTargets.NormalLights,
                null,
                PipelineType.Light,
                world,
                ref commandBuffer,
                renderDestination.Size(),
                camera.ZoomedBounds,
                _lightTextureSamplerBindings
            );

            // render light to game
            renderer.DrawSprite(renderTargets.NormalLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Multiply);
        }

        if (World.RimLightsEnabled)
        {
            _rimLightTextureSamplerBindings[1] = new TextureSamplerBinding(renderTargets.LightBase, SpriteBatch.LinearClamp);
            renderer.Clear(ref commandBuffer, renderTargets.RimLights, Color.Transparent);
            DrawAllLights(
                renderer,
                renderTargets.LightBase, // not used by rim light shader
                renderTargets.RimLights,
                null,
                PipelineType.RimLight,
                world,
                ref commandBuffer,
                renderDestination.Size(),
                camera.ZoomedBounds,
                _rimLightTextureSamplerBindings
            );

            // render rim light to game
            renderer.DrawSprite(renderTargets.RimLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Additive);
        }
    }

    private void DrawAllLights(Renderer renderer, Texture lightTexture, Texture renderTarget, Color? clearColor, PipelineType pipelineType, World world,
        ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds, TextureSamplerBinding[] fragmentBindings)
    {
        world.Entities.FindAll(_lights);
        for (var i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (!light.IsEnabled)
                continue;
            if (!light.Bounds.Intersects(cameraBounds))
                continue;

            renderer.DrawSprite(
                lightTexture,
                Matrix3x2.Identity,
                Color.White
            );

            var vertUniform = Renderer.GetOrthographicProjection(renderDestinationSize.X, renderDestinationSize.Y);
            var fragUniform = new Pipelines.RimLightUniforms()
            {
                LightColor = new Vector3(light.Color.R / 255f, light.Color.G / 255f, light.Color.B / 255f),
                LightIntensity = light.Intensity,
                LightRadius = Math.Max(light.Width, light.Height) * 0.5f,
                LightPos = light.Position + light.Size.ToVec2() * light.Pivot,
                VolumetricIntensity = light.VolumetricIntensity,
                RimIntensity = light.RimIntensity,
                Angle = light.Angle,
                ConeAngle = light.ConeAngle,

                TexelSize = new Vector4(
                    1.0f / renderDestinationSize.X,
                    1.0f / renderDestinationSize.Y,
                    renderDestinationSize.X,
                    renderDestinationSize.Y
                ),
                Bounds = new Vector4(
                    MathF.Floor(cameraBounds.Min.X),
                    MathF.Floor(cameraBounds.Min.Y),
                    cameraBounds.Width,
                    cameraBounds.Height
                ),
            };

            renderer.UpdateBuffers(ref commandBuffer);
            renderer.BeginRenderPass(ref commandBuffer, renderTarget, clearColor, pipelineType);
            renderer.DrawIndexedSprites(ref commandBuffer, vertUniform, fragUniform, fragmentBindings, true);
            renderer.EndRenderPass(ref commandBuffer);
        }
    }
}

public class MenuRenderPass : RenderPass
{
    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        renderer.Clear(ref commandBuffer, Shared.Game.RenderTargets.MenuRender.Target, Color.Transparent);
        renderer.DrawRect(new Vector2(0, 0), new Vector2(1, 1), Color.Black);
        Shared.Menus.Draw(renderer, alpha);
        renderer.RunRenderPass(ref commandBuffer, Shared.Game.RenderTargets.MenuRender.Target, Color.Transparent, null);
        renderer.DrawSprite(Shared.Game.RenderTargets.MenuRender.Target, Matrix3x2.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }
}

public class LoadingScreenRenderPass : RenderPass
{
    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        Shared.LoadingScreen.Draw(renderer, ref commandBuffer, renderDestination,
            alpha);
    }
}

public class DebugRenderPass : RenderPass
{
    private void DrawViewBounds(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        // if (!World.Debug)
        return;

        var bounds = Shared.Game.Camera.ZoomedBounds;
        var view = Shared.Game.Camera.GetView(0);
        var min = Vector2.Transform(bounds.Min, view);
        var max = Vector2.Transform(bounds.Max, view);
        renderer.DrawRectOutline(min, max, Color.LimeGreen);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }
}
