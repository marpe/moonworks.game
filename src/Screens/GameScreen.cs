using MyGame.Cameras;
using MyGame.Components;
using MyGame.Graphics;
using MyGame.Utils;

namespace MyGame.Screens;

public class LdtkProject
{
    public Dictionary<long, Texture> Textures;
    public LdtkJson LdtkJson;

    public LdtkProject(LdtkJson ldtkJson, Dictionary<long, Texture> textures)
    {
        LdtkJson = ldtkJson;
        Textures = textures;
    }

    private static Point WorldToTilePosition(Vector2 worldPosition, int gridSize, long width, long height)
    {
        var x = MathF.FastFloorToInt(worldPosition.X / gridSize);
        var y = MathF.FastFloorToInt(worldPosition.Y / gridSize);
        return new Point((int)MathF.Clamp(x, 0, width - 1), (int)MathF.Clamp(y, 0, height - 1));
    }

    public void Draw(Renderer renderer)
    {
        var level = LdtkJson.Levels[0];
        var levelPosition = new Vector2(level.WorldX, level.WorldY);

        var cameraBounds = new Rectangle(0, 0, 1920, 1080);

        for (var iLvl = level.LayerInstances.Length - 1; iLvl >= 0; iLvl--)
        {
            var layer = level.LayerInstances[iLvl];
            if (!layer.TilesetDefUid.HasValue)
                continue;

            var texture = Textures[layer.TilesetDefUid.Value];

            var layerWidth = layer.CWid;
            var layerHeight = layer.CHei;

            var min = WorldToTilePosition(cameraBounds.MinVec() - levelPosition, (int)layer.GridSize, layerWidth, layerHeight);
            var max = WorldToTilePosition(cameraBounds.MaxVec() - levelPosition, (int)layer.GridSize, layerWidth, layerHeight);

            for (var i = 0; i < layer.GridTiles.Length; i++)
            {
                var tile = layer.GridTiles[i];
                RenderTile(renderer, tile, layer, levelPosition, texture);
            }

            for (var i = 0; i < layer.AutoLayerTiles.Length; i++)
            {
                var tile = layer.AutoLayerTiles[i];
                RenderTile(renderer, tile, layer, levelPosition, texture);
            }

            /*for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    // var tile = layer.Tiles[x, y];
                    // if (tile == null)
                        // continue;
                    // RenderTile(batcher, tile, layer.LayerInstance, levelPosition, texture);
                }
            }*/
        }
    }

    private static void RenderTile(Renderer renderer, TileInstance tile, LayerInstance layer, Vector2 levelPosition, Texture texture)
    {
        var tilePosition = new Vector2(tile.Px[0], tile.Px[1]);
        var srcRect = new Rectangle((int)tile.Src[0], (int)tile.Src[1], (int)layer.GridSize, (int)layer.GridSize);
        var destRect = new Rectangle(
            (int)(levelPosition.X + tilePosition.X),
            (int)(levelPosition.Y + tilePosition.Y),
            (int)layer.GridSize,
            (int)layer.GridSize
        );
        var sprite = new Sprite(texture, srcRect);
        var transform = Matrix3x2.CreateTranslation(destRect.X, destRect.Y);
        renderer.DrawSprite(sprite, transform, Color.White, 0);
    }

    public void Dispose()
    {
        foreach (var (key, texture) in Textures)
        {
            texture.Dispose();
        }

        Textures.Clear();
    }
}

public class GameScreen
{
    private Camera _camera;
    private MyGameMain _game;
    private GraphicsDevice _device;
    private CameraController _cameraController;
    private LdtkProject? _ldtkProject;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadLDtk();

        _camera = new Camera();
        _cameraController = new CameraController(_camera);
    }

    private void LoadLDtk()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var ldtkPath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.MapLdtk);
            var jsonString = File.ReadAllText(ldtkPath);

            var ldtkJson = LdtkJson.FromJson(jsonString);
            var textures = LoadTextures(_game.GraphicsDevice, ldtkPath, ldtkJson.Defs.Tilesets);

            _ldtkProject = new LdtkProject(ldtkJson, textures);

            Logger.LogInfo($"Loaded LDtk in {sw2.ElapsedMilliseconds} ms");
        });
    }


    private static Dictionary<long, Texture> LoadTextures(GraphicsDevice device, string ldtkPath, TilesetDefinition[] tilesets)
    {
        var textures = new Dictionary<long, Texture>();

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var tilesetDef in tilesets)
        {
            if (string.IsNullOrWhiteSpace(tilesetDef.RelPath))
                continue;
            var tilesetPath = Path.Combine(Path.GetDirectoryName(ldtkPath) ?? "", tilesetDef.RelPath);
            if (tilesetPath.EndsWith(".aseprite"))
            {
                var asepriteTexture = TextureUtils.LoadAseprite(device, tilesetPath);
                textures.Add(tilesetDef.Uid, asepriteTexture);
            }
            else
            {
                var texture = Texture.LoadPNG(device, commandBuffer, tilesetPath);
                textures.Add(tilesetDef.Uid, texture);
            }
        }

        device.Submit(commandBuffer);

        return textures;
    }

    public void Unload()
    {
        _ldtkProject?.Dispose();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;
        _cameraController.Update(deltaSeconds, input, allowMouseInput, allowKeyboardInput);
    }

    public void Draw(Renderer renderer)
    {
        _ldtkProject?.Draw(renderer);

        if (_ldtkProject != null)
        {
            var width = _ldtkProject.LdtkJson.Levels[0].PxWid;
            var height = _ldtkProject.LdtkJson.Levels[0].PxHei;
            DrawRect(renderer, Vector2.Zero, new Vector2(width, height), Color.Magenta, 1.0f);
        }

        _camera.Size = _game.MainWindow.Size;

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        renderer.FlushBatches(renderer.SwapTexture, _cameraController.ViewProjection, renderer.DefaultClearColor);
    }

    private static void DrawRect(Renderer renderer, Vector2 min, Vector2 max, Color color, float thickness)
    {
        ReadOnlySpan<Vector2> points = stackalloc Vector2[]
        {
            min,
            new(max.X, min.Y),
            max,
            new(min.X, max.Y)
        };
        for (var i = 0; i < 4; i++)
        {
            renderer.DrawLine(points[i], points[(i + 1) % 4], color, thickness);
        }
    }
}
