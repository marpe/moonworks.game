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

            ConsoleToast.Draw(renderer, ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender);

            if (!Shared.Game.ConsoleScreen.IsHidden)
            {
                Shared.Game.ConsoleScreen.Draw(renderer, ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, alpha);
            }
        }

        if (_hasRenderedConsole)
        {
            renderer.DrawSprite(Shared.Game.RenderTargets.ConsoleRender.Target, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
        }

        Shared.Game._fpsDisplay.DrawFPS(renderer, commandBuffer, renderDestination);
    }
}

public class WorldRenderPass : RenderPass
{
    private List<Light> _lights = new();
    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        DrawWorld(renderer, ref commandBuffer, Shared.Game.RenderTargets.GameRender, alpha);
        if (RenderTargets.RenderScale != 1)
        {
            var camera = Shared.Game.Camera;
            var dstSize = Shared.Game.RenderTargets.CompositeRender.Size / (int)RenderTargets.RenderScale;
            // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
            var srcRect = new Bounds(camera.FloorRemainder.X, camera.FloorRemainder.Y, dstSize.X, dstSize.Y);
            var gameRenderSprite = new Sprite(Shared.Game.RenderTargets.GameRender.Target, srcRect);
            var scale = Matrix3x2.CreateScale((int)RenderTargets.RenderScale, (int)RenderTargets.RenderScale).ToMatrix4x4();
            renderer.DrawSprite(gameRenderSprite, scale, Color.White, 0, SpriteFlip.None, false);
        }
        else
        {
            renderer.DrawSprite(Shared.Game.RenderTargets.GameRender.Target, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None, true);
        }
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, PipelineType.Sprite);
    }

    private void DrawWorld(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (!Shared.Game.World.IsLoaded)
        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            return;
        }

        renderer.Clear(ref commandBuffer, renderDestination, Color.Black);

        Shared.Game.World.Draw(renderer, Shared.Game.Camera, alpha, MyGameMain.UsePointFiltering);

        Shared.Game.World.DrawDebug(renderer, Shared.Game.Camera, alpha);

        var viewProjection = Shared.Game.Camera.GetViewProjection(renderDestination.Width, renderDestination.Height, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection, PipelineType.PixelArt);


        if (MyGameMain.LightsEnabled)
            DrawLights(Shared.Game.World, renderer, ref commandBuffer, renderDestination, Shared.Game.Camera, alpha, MyGameMain.UsePointFiltering);
    }

    private void DrawLights(World world, Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, Camera camera, double alpha, bool usePointFiltering)
    {
        DrawLightBaseLayer(world, renderer, ref commandBuffer, Shared.Game.RenderTargets.LightBase, camera, alpha, usePointFiltering);

        // render lights
        renderer.DrawSprite(Shared.Game.RenderTargets.LightBase, Matrix4x4.Identity, Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, Shared.Game.RenderTargets.NormalLights, world.AmbientColor, PipelineType.Light);
        DrawAllLights(
            world,
            ref commandBuffer,
            renderDestination.Size(),
            camera.ZoomedBounds,
            new[]
            {
                new TextureSamplerBinding(Shared.Game.RenderTargets.LightBase, Renderer.PointClamp),
            }
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render light to game
        renderer.DrawSprite(Shared.Game.RenderTargets.NormalLights, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, PipelineType.Multiply);

        // render rim
        renderer.DrawSprite(renderer.BlankSprite, Matrix3x2.CreateScale(renderDestination.Width, renderDestination.Height).ToMatrix4x4(), Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, Shared.Game.RenderTargets.RimLights, Color.Transparent, PipelineType.RimLight);
        DrawAllLights(
            world,
            ref commandBuffer,
            renderDestination.Size(),
            camera.ZoomedBounds,
            new[]
            {
                new TextureSamplerBinding(renderer.BlankSprite.TextureSlice.Texture, Renderer.PointClamp),
                new TextureSamplerBinding(Shared.Game.RenderTargets.LightBase, Renderer.PointClamp),
            }
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render rim light to game
        renderer.DrawSprite(Shared.Game.RenderTargets.RimLights, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, PipelineType.Additive);
    }

    private void DrawLightBaseLayer(World world, Renderer renderer, ref CommandBuffer commandBuffer, RenderTarget renderDestination, Camera camera, double alpha,
        bool usePointFiltering)
    {
        LevelRenderer.DrawLevel(renderer, world, world.Root, world.Level, camera.ZoomedBounds, usePointFiltering, drawBackground: false);
        world.DrawEntities(renderer, alpha, usePointFiltering);

        var viewProjection = camera.GetViewProjection(renderDestination.Width, renderDestination.Height, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Transparent, viewProjection, PipelineType.PixelArt);
    }

    private void DrawAllLights(World world, ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds,
        TextureSamplerBinding[] fragmentBindings)
    {
        var tempCameraBounds = cameraBounds;
        var tempCommandBuffer = commandBuffer;
        world.Entities.FindAll(_lights);
        for(var i = 0; i < _lights.Count; i++)
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
        Shared.Menus.Draw(renderer, ref commandBuffer, Shared.Game.RenderTargets.MenuRender, alpha);

        renderer.DrawSprite(Shared.Game.RenderTargets.MenuRender.Target, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None, false);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, PipelineType.Sprite);
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
        /*if (!World.Debug) */return;

        renderer.DrawRectOutline(Vector2.Zero, Shared.Game.RenderTargets.CompositeRender.Size, Color.LimeGreen, 1f);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }
}
