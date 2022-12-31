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

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if ((int)Shared.Game.Time.UpdateCount % ConsoleSettings.RenderRate == 0)
        {
            _hasRenderedConsole = true;
            renderer.Clear(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, Color.Transparent);
            renderer.DrawRect(new Vector2(0, 0), new Vector2(1, 1), Color.Black);

            ConsoleToast.Draw(renderer, Shared.Game.RenderTargets.ConsoleRender);

            if (Shared.Game.ConsoleScreen.ConsoleScreenState != ConsoleScreenState.Hidden)
            {
                Shared.Game.ConsoleScreen.Draw(renderer, alpha);
            }

            renderer.RunRenderPass(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, null, null, true);
        }

        if (_hasRenderedConsole)
        {
            renderer.DrawSprite(Shared.Game.RenderTargets.ConsoleRender, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true);
        }

        Shared.Game._fpsDisplay.DrawFPS(renderer, renderDestination.Size(), FPSDisplayPosition.BottomRight);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true);
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
        var isSameSize = (renderTargets.GameRender.Width == renderTargets.CompositeRender.Width &&
                          renderTargets.GameRender.Height == renderTargets.CompositeRender.Height);
        if (!isSameSize)
        {
            var camera = Shared.Game.Camera;
            var srcSize = renderTargets.GameRender.Size - UPoint.One;
            // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
            var srcRect = new Bounds(camera.FloorRemainder.X, camera.FloorRemainder.Y, srcSize.X, srcSize.Y);
            var gameRenderSprite = new Sprite(renderTargets.GameRender.Target, srcRect);
            var scale = renderTargets.CompositeRender.Size / srcSize;
            var t = Matrix3x2.CreateScale(scale, scale).ToMatrix4x4();
            renderer.DrawSprite(gameRenderSprite, t, Color.White, 0, SpriteFlip.None);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, false, PipelineType.PixelArt);
        }
        else
        {
            renderer.DrawSprite(renderTargets.GameRender.Target, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, true, PipelineType.Sprite);
        }
    }

    private void DrawWorld(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var world = Shared.Game.World;
        if (!world.IsLoaded)
        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            return;
        }

        var camera = Shared.Game.Camera;
        var viewProjection = camera.GetViewProjection(480, 270, alpha);
        var renderTargets = Shared.Game.RenderTargets;
        var isSameSize = (renderTargets.GameRender.Width == renderTargets.CompositeRender.Width &&
                          renderTargets.GameRender.Height == renderTargets.CompositeRender.Height);
        var pipeline = !isSameSize ? PipelineType.Sprite : PipelineType.PixelArt;
        var usePointFiltering = pipeline == PipelineType.Sprite;
        
        LevelRenderer.DrawLevel(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        world.DrawEntities(renderer, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LightBase, Color.Transparent, viewProjection, usePointFiltering, pipeline);

        LevelRenderer.DrawBackground(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        world.DrawDebug(renderer, camera, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Transparent, viewProjection, usePointFiltering, pipeline);
        
        renderer.DrawSprite(renderTargets.LightBase, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true);

        if (World.LightsEnabled)
        {
            DrawLights(world, renderer, ref commandBuffer, renderTargets.LightBase, renderTargets.NormalLights, camera.ZoomedBounds);

            // render light to game
            renderer.DrawSprite(renderTargets.NormalLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Multiply);
        }

        if (World.RimLightsEnabled)
        {
            DrawRimLight(world, renderer, ref commandBuffer, renderTargets.LightBase, renderTargets.RimLights, camera.ZoomedBounds);

            // render rim light to game
            renderer.DrawSprite(renderTargets.RimLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Additive);
        }
    }

    private void DrawLights(World world, Renderer renderer, ref CommandBuffer commandBuffer, Texture lightBase, Texture renderDestination, Bounds cameraBounds)
    {
        // render lights
        renderer.DrawSprite(renderer.BlankSprite, Matrix3x2.CreateScale(renderDestination.Width, renderDestination.Height).ToMatrix4x4(), Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, world.AmbientColor, PipelineType.Light);
        _lightTextureSamplerBindings[0] = new TextureSamplerBinding(lightBase, SpriteBatch.PointClamp);
        DrawAllLights(
            world,
            ref commandBuffer,
            renderDestination.Size(),
            cameraBounds,
            _lightTextureSamplerBindings
        );
        renderer.EndRenderPass(ref commandBuffer);
    }

    private void DrawRimLight(World world, Renderer renderer, ref CommandBuffer commandBuffer, Texture lightBase, Texture renderDestination,
        Bounds cameraBounds)
    {
        // render rim
        renderer.DrawSprite(renderer.BlankSprite, Matrix3x2.CreateScale(renderDestination.Width, renderDestination.Height).ToMatrix4x4(), Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, Color.Transparent, PipelineType.RimLight);
        _rimLightTextureSamplerBindings[0] = new TextureSamplerBinding(renderer.BlankSprite.TextureSlice.Texture, SpriteBatch.PointClamp);
        _rimLightTextureSamplerBindings[1] = new TextureSamplerBinding(lightBase, SpriteBatch.PointClamp);
        DrawAllLights(
            world,
            ref commandBuffer,
            renderDestination.Size(),
            cameraBounds,
            _rimLightTextureSamplerBindings
        );
        renderer.EndRenderPass(ref commandBuffer);
    }

    private void DrawAllLights(World world, ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds,
        TextureSamplerBinding[] fragmentBindings)
    {
        var tempCameraBounds = cameraBounds;
        var tempCommandBuffer = commandBuffer;
        world.Entities.FindAll(_lights);
        for (var i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (!light.IsEnabled)
                continue;
            if (!light.Bounds.Intersects(tempCameraBounds))
                continue;
            var vertUniform = Renderer.GetViewProjection(renderDestinationSize.X, renderDestinationSize.Y);
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
                    tempCameraBounds.Min.X,
                    tempCameraBounds.Min.Y,
                    tempCameraBounds.Width,
                    tempCameraBounds.Height
                ),
            };
            var fragmentParamOffset = tempCommandBuffer.PushFragmentShaderUniforms(fragUniform);
            var vertexParamOffset = tempCommandBuffer.PushVertexShaderUniforms(vertUniform);
            tempCommandBuffer.BindFragmentSamplers(fragmentBindings);
            SpriteBatch.DrawIndexedQuads(ref tempCommandBuffer, 0, 1, vertexParamOffset, fragmentParamOffset);
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
        renderer.RunRenderPass(ref commandBuffer, Shared.Game.RenderTargets.MenuRender.Target, Color.Transparent, null, true);
        renderer.DrawSprite(Shared.Game.RenderTargets.MenuRender.Target, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Sprite);
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
        if (!World.Debug)
            return;

        var bounds = Shared.Game.Camera.ZoomedBounds;
        var view = Shared.Game.Camera.GetView(0);
        var min = Vector2.Transform(bounds.Min, view);
        var max = Vector2.Transform(bounds.Max, view);
        renderer.DrawRectOutline(min, max, Color.LimeGreen, 1f);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true);
    }

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }
}
