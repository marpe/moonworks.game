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
    private ulong _lastUpdateCount;

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (_lastUpdateCount < Shared.Game.Time.UpdateCount)
        {
            _lastUpdateCount = Shared.Game.Time.UpdateCount;
            _hasRenderedConsole = true;
            renderer.Clear(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, Color.Transparent);
            renderer.DrawRect(new Vector2(0, 0), new Vector2(1, 1), Color.Black);

            ConsoleToast.Draw(renderer, (int)Shared.Game.RenderTargets.ConsoleRender.Target.Height);

            if (Shared.Game.ConsoleScreen.ConsoleScreenState != ConsoleScreenState.Hidden)
            {
                Shared.Game.ConsoleScreen.Draw(renderer, alpha);
            }

            Shared.Game._fpsDisplay.DrawFPS(renderer, Shared.Game.RenderTargets.ConsoleRender.Size);
            renderer.RunRenderPass(ref commandBuffer, Shared.Game.RenderTargets.ConsoleRender, null, null);
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
    private LightUniform _lightUniform = new();
    private List<LightU> _lightUniforms = new();
    private List<LightU2> _lightUniforms2 = new();

    private TextureSamplerBinding[] _lightTextureSamplerBindings = new TextureSamplerBinding[1];
    private TextureSamplerBinding[] _rimLightTextureSamplerBindings = new TextureSamplerBinding[2];

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var renderTargets = Shared.Game.RenderTargets;
        DrawWorld(renderer, ref commandBuffer, renderTargets.GameRender, alpha);

        var camera = Shared.Game.Camera;
        // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
        camera.GetViewFloored(alpha, out var floorRemainder);

        var srcPos = floorRemainder * renderTargets.GameScale;
        var srcSize = renderTargets.GameRender.Size - UPoint.One * (uint)renderTargets.GameScale;
        var srcRect = new Bounds(srcPos.X, srcPos.Y, srcSize.X, srcSize.Y);
        var dstSize = renderDestination.Size();
        var dstRect = new Bounds(0, 0, dstSize.X, dstSize.Y);

        renderer.DrawSprite(renderTargets.GameRender, srcRect, dstRect, Color.White);

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null, false, PipelineType.PixelArt);
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
                   Matrix3x2.CreateScale(renderTargets.GameScale);
        var projection = Renderer.GetOrthographicProjection(renderTargets.GameRender.Width, renderTargets.GameRender.Height);
        var viewProjection = view.ToMatrix4x4() * projection;

        LevelRenderer.DrawLevel(renderer, world, world.Root, world.Level, camera.ZoomedBounds);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LevelBase, Color.Transparent, viewProjection);

        world.DrawEntities(renderer, alpha);
        renderer.RunRenderPass(ref commandBuffer, renderTargets.LevelBase, null, viewProjection, false, PipelineType.PixelArt);

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

            DrawAllLights2(
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
            // TODO (marpe): Experiment with sampling for light layers
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.LightsToMain);
        }

        if (World.RimLightsEnabled)
        {
            _rimLightTextureSamplerBindings[1] = new TextureSamplerBinding(renderTargets.LightBase, SpriteBatch.LinearClamp);
            renderer.Clear(ref commandBuffer, renderTargets.RimLights, Color.Transparent);
            DrawAllLights2(
                renderer,
                renderer.BlankSprite.TextureSlice.Texture, // not used by rim light shader
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
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.RimLightsToMain);
        }
    }

    private void DrawAllLights(Renderer renderer, Texture lightTexture, Texture renderTarget, Color? clearColor, PipelineType pipelineType, World world,
        ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds, TextureSamplerBinding[] fragmentBindings)
    {
        world.Entities.FindAll(_lights);

        _lightUniforms2.Clear();
        for (var i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (!light.IsEnabled)
                continue;
            if (!light.Bounds.Intersects(cameraBounds))
                continue;
            _lightUniforms2.Add(new LightU2()
            {
                LightColor = light.Color.ToVector3(),
                LightIntensity = light.Intensity,
                VolumetricIntensity = light.VolumetricIntensity,
                Angle = light.Angle,
                ConeAngle = light.ConeAngle,
            });
            renderer.DrawSprite(renderer.BlankSprite.TextureSlice.Texture, null, light.Bounds, Color.White);
        }

        var camera = Shared.Game.Camera;
        var renderTargets = Shared.Game.RenderTargets;
        var view = camera.GetViewFloored(0, out _) *
                   Matrix3x2.CreateScale(renderTargets.GameScale);
        var projection = Renderer.GetOrthographicProjection(renderTarget.Width, renderTarget.Height);
        var viewProjection = view.ToMatrix4x4() * projection;

        renderer.UpdateBuffers(ref commandBuffer);
        renderer.BeginRenderPass(ref commandBuffer, renderTarget, clearColor, pipelineType);
        for (var i = 0; i < renderer.SpriteBatch.NumSprites; i++)
        {
            var vertUniform = viewProjection;
            var fragUniform = _lightUniforms2[i];
            renderer.SpriteBatch.DrawIndexed(ref commandBuffer, vertUniform, fragUniform, fragmentBindings, false, i, 1);
        }

        renderer.SpriteBatch.Discard();
        renderer.EndRenderPass(ref commandBuffer);
    }

    private unsafe void DrawAllLights2(Renderer renderer, Texture lightTexture, Texture renderTarget, Color? clearColor, PipelineType pipelineType, World world,
        ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds, TextureSamplerBinding[] fragmentBindings)
    {
        world.Entities.FindAll(_lights);

        _lightUniform.Scale = Shared.Game.RenderTargets.GameScale;
        _lightUniform.TexelSize = new Vector4(
            1.0f / renderDestinationSize.X,
            1.0f / renderDestinationSize.Y,
            renderDestinationSize.X,
            renderDestinationSize.Y
        );
        _lightUniform.Bounds = new Vector4(
            MathF.Floor(cameraBounds.Min.X),
            MathF.Floor(cameraBounds.Min.Y),
            cameraBounds.Width,
            cameraBounds.Height
        );

        _lightUniforms.Clear();
        _lightUniform.NumLights = 0;

        for (var i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (!light.IsEnabled)
                continue;
            if (!light.Bounds.Intersects(cameraBounds))
                continue;

            var lightUniform = new LightU
            {
                LightColor = new Vector3(light.Color.R / 255f, light.Color.G / 255f, light.Color.B / 255f),
                LightIntensity = light.Intensity,
                LightRadius = Math.Max(light.Width, light.Height) * 0.5f,
                LightPos = light.Position + light.Size.ToVec2() * light.Pivot,
                VolumetricIntensity = light.VolumetricIntensity,
                RimIntensity = light.RimIntensity,
                Angle = light.Angle,
                ConeAngle = light.ConeAngle,
            };

            _lightUniforms.Add(lightUniform);
        }

        renderer.DrawSprite(
            lightTexture,
            null,
            renderTarget.Bounds(),
            Color.White
        );

        renderer.UpdateBuffers(ref commandBuffer);

        renderer.BeginRenderPass(ref commandBuffer, renderTarget, clearColor, pipelineType);
        var vertUniform = Renderer.GetOrthographicProjection(renderTarget.Width, renderTarget.Height);

        while (_lightUniforms.Count > 0)
        {
            var numLights = Math.Min(_lightUniforms.Count, LightUniform.MaxNumLights);
            fixed (byte* lights = _lightUniform.Lights)
            {
                var lightsPtr = (LightU*)lights;
                for (var i = numLights - 1; i >= 0; i--)
                {
                    lightsPtr[i] = _lightUniforms[i];
                    _lightUniforms.RemoveAt(i);
                }
            }

            _lightUniform.NumLights = numLights;
            renderer.SpriteBatch.DrawIndexed(ref commandBuffer, vertUniform, _lightUniform, fragmentBindings, false, 0, renderer.SpriteBatch.NumSprites);
        }

        renderer.SpriteBatch.Discard();
        renderer.EndRenderPass(ref commandBuffer);
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
