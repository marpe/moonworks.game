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
        var isSameSize = (renderTargets.GameRender.Width - renderTargets.RenderScale == renderTargets.CompositeRender.Width &&
                          renderTargets.GameRender.Height - renderTargets.RenderScale == renderTargets.CompositeRender.Height);
        if (!isSameSize)
        {
            var camera = Shared.Game.Camera;
            var srcSize = renderTargets.GameRender.Size - UPoint.One;
            var renderScale = renderTargets.RenderScale;
            // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
            camera.GetViewFloored(alpha, out var floorRemainder);
            var srcRect = new Bounds(floorRemainder.X, floorRemainder.Y, srcSize.X, srcSize.Y);
            var gameRenderSprite = new Sprite(renderTargets.GameRender.Target, srcRect);
            var dstSize = renderTargets.CompositeRender.Size + (uint)renderScale;
            var scale = new Vector2((float)dstSize.X / srcSize.X, (float)dstSize.Y / srcSize.Y);
            var t = Matrix3x2.CreateScale(scale.X, scale.Y).ToMatrix4x4();
            renderer.DrawSprite(gameRenderSprite, t, Color.White, 0, SpriteFlip.None);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, false, PipelineType.PixelArt);
        }
        else
        {
            var camera = Shared.Game.Camera;
            camera.GetViewFloored(alpha, out var floorRemainder);
            /*var renderScale = renderTargets.RenderScale;
            // var srcSize = new UPoint((uint)(renderTargets.GameSize.X * renderScale), (uint)(renderTargets.GameSize.Y * renderScale));
            var srcSize = renderTargets.GameRender.Size - (uint)renderScale; 
            var srcRect = new Bounds(floorRemainder.X, floorRemainder.Y, renderTargets.GameSize.X * renderScale, renderTargets.GameSize.Y * renderScale);
            var gameRenderSprite = new Sprite(renderTargets.GameRender.Target, srcRect);
            var dstSize = renderTargets.CompositeRender.Size + (uint)renderScale;
            var scale = new Vector2((float)dstSize.X / srcSize.X, (float)dstSize.Y / srcSize.Y);
            var t = Matrix3x2.CreateScale(scale.X, scale.Y).ToMatrix4x4();*/
            var transform = (
                Matrix3x2.CreateTranslation(-floorRemainder * renderTargets.RenderScale) //* Matrix3x2.CreateTranslation(-0.5f, -0.5f)
            );
            renderer.DrawSprite(renderTargets.GameRender, transform, Color.White, 0, SpriteFlip.None);
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
        var renderTargets = Shared.Game.RenderTargets;

        var flooredView = camera.GetViewFloored(alpha, out var floorRemainder);

        LevelRenderer.DrawLevel(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        world.DrawEntities(renderer, alpha);


        var view = flooredView *
                   Matrix3x2.CreateScale(renderTargets.RenderScale).ToMatrix4x4();
        var viewProjection = view * Renderer.GetViewProjection(renderTargets.LevelBase.Width, renderTargets.LevelBase.Height);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LevelBase, Color.Transparent, viewProjection, true, PipelineType.Sprite);

        renderer.DrawSprite(renderTargets.LevelBase, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LightBase, Color.Transparent, null, true, PipelineType.Sprite);


        LevelRenderer.DrawBackground(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        var backgroundTransform = view *
                                  Matrix3x2.CreateTranslation(-0.5f, -0.5f).ToMatrix4x4() *
                                  Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height);
        world.DrawDebug(renderer, camera, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Transparent, backgroundTransform, true, PipelineType.Sprite);

        // var levelBaseSprite = new Sprite(renderTargets.LevelBase.Target, new Bounds(floorRemainder.X, floorRemainder.Y, projectionSize.X, projectionSize.Y));
        renderer.DrawSprite(renderTargets.LevelBase, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, false, PipelineType.Sprite);

        var flooredMin = camera.ZoomedBounds.Min.Floor();
        var flooredBounds = new Bounds(
            flooredMin.X,
            flooredMin.Y,
            camera.ZoomedBounds.Width,
            camera.ZoomedBounds.Height
        );

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
                flooredBounds,
                _lightTextureSamplerBindings
            );

            // render light to game
            renderer.DrawSprite(renderTargets.NormalLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, false, PipelineType.Multiply);
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
                flooredBounds,
                _rimLightTextureSamplerBindings
            );

            // render rim light to game
            renderer.DrawSprite(renderTargets.RimLights, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, false, PipelineType.Additive);
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
                Matrix4x4.Identity,
                Color.White,
                0,
                SpriteFlip.None
            );

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
                    cameraBounds.Min.X,
                    cameraBounds.Min.Y,
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
        // if (!World.Debug)
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
