using MoonWorks.Audio;
using MyGame.Fonts;
using MyGame.WorldsRoot;

namespace MyGame.Utils;

public class TextureAsset
{
    public TextureSlice TextureSlice;

    public TextureAsset(TextureSlice textureSlice)
    {
        TextureSlice = textureSlice;
    }
}

public class AsepriteAsset : TextureAsset
{
    public AsepriteFile AsepriteFile;

    public AsepriteAsset(TextureSlice textureSlice, AsepriteFile aseprite) : base(textureSlice)
    {
        AsepriteFile = aseprite;
    }
}

public class ContentManager
{
    public static ColorConverter ColorConverter = new();

    public static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Converters = { ColorConverter },
        Formatting = Formatting.None
    };

    public static readonly JsonSerializer JsonSerializer = new()
    {
        Converters = { ColorConverter }
    };

    private readonly MyGameMain _game;
    private static StringBuilder _sb = new();

    private Dictionary<string, object> _loadedAssets = new();

    private object lockObject = new object();

    // private Dictionary<string, IDisposable> _disposableAssets = new();

    public ContentManager(MyGameMain game)
    {
        _game = game;
    }

    public T Load<T>(string assetName, bool forceReload = false)
    {
        T? result;

        lock (lockObject)
        {
            if (forceReload)
            {
                _loadedAssets.Remove(assetName, out var oldAsset);
                if (oldAsset is IDisposable disposable)
                    disposable.Dispose();
            }

            if (!_loadedAssets.TryGetValue(assetName, out var asset))
            {
                asset = InternalLoad<T>(assetName) ?? throw new Exception();
                _loadedAssets.Add(assetName, asset);
                result = (T)asset;
                Logs.LogInfo($"[{Shared.Game.Time.UpdateCount}] Loaded {typeof(T).Name} asset \"{assetName}\"");
            }
            else
            {
                result = (T)asset;
            }
        }

        return result ?? throw new Exception();
    }

    public void PackTextures()
    {
        Logs.LogInfo($"[{Shared.Game.Time.UpdateCount}] Starting atlas packing");
        var sw = Stopwatch.StartNew();
        var packer = new RectPacker(2048, 2048);

        var device = Shared.Content._game.GraphicsDevice;
        var atlasTexture =
            Texture.CreateTexture2D(device, 2048, 2048, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        var dstX = 0;
        var dstY = 0;

        var cb = device.AcquireCommandBuffer();

        var numPackedTextures = 0;
        var packedTextures = new List<string>();

        foreach (var (key, value) in _loadedAssets)
        {
            if (value is not TextureAsset textureAsset)
                continue;

            var textureSlice = textureAsset.TextureSlice;

            if (packer.AddRect(textureSlice.Rectangle.W, textureSlice.Rectangle.H, ref dstX, ref dstY))
            {
                var newSlice = new TextureSlice(atlasTexture,
                    new Rect(dstX, dstY, textureSlice.Rectangle.W, textureSlice.Rectangle.H));
                _loadedAssets[key] = newSlice;
                cb.CopyTextureToTexture(textureSlice, newSlice, Filter.Nearest);
            }
            else
            {
                throw new Exception("Atlas full");
            }

            numPackedTextures++;
            packedTextures.Add(key);
        }

        device.Submit(cb);

        // TextureUtils.SaveTexture("packed.png", device, atlasTexture);

        Logs.LogInfo($"Packed {numPackedTextures.ToString()} textures:\n{string.Join('\n', packedTextures)}");
        sw.StopAndLog(nameof(PackTextures));
    }

    private static object InternalLoad<T>(string assetName)
    {
        var t = typeof(T);
        if (t == typeof(RootJson))
        {
            var json = File.ReadAllText(assetName);
            var root = JsonConvert.DeserializeObject<RootJson>(json, JsonSerializerSettings) ??
                       throw new InvalidOperationException();
            return root;
        }

        if (t == typeof(TextureAsset) ||
            t == typeof(AsepriteAsset))
        {
            var extension = Path.GetExtension(assetName);
            if (extension == ".aseprite")
                return LoadAseprite(assetName, Shared.Content._game.GraphicsDevice);
            return LoadTexture(assetName, Shared.Content._game.GraphicsDevice);
        }

        if (t == typeof(AudioBuffer))
        {
            return AudioDataWav.CreateBuffer(Shared.Content._game.AudioDevice, assetName);
        }

        if (t == typeof(BMFont))
        {
            return BMFont.LoadFromFile(Shared.Content._game.GraphicsDevice, assetName);
        }

        throw new Exception($"Unsupported asset type \"{t.Name}\" ({assetName})");
    }

    private static AsepriteAsset LoadAseprite(string assetName, GraphicsDevice device)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var (texture, aseprite) = TextureUtils.LoadAseprite(device, ref commandBuffer, assetName);
        device.Submit(commandBuffer);
        return new AsepriteAsset(texture, aseprite);
    }

    private static TextureAsset LoadTexture(string assetName, GraphicsDevice device)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.FromImageFile(device, commandBuffer, assetName);
        device.Submit(commandBuffer);
        return new TextureAsset(texture);
    }

    [ConsoleHandler("content_list")]
    public static void ListLoadedAssets()
    {
        _sb.Clear();

        var assetsByType = Shared.Content._loadedAssets.GroupBy((kvp) => kvp.Value.GetType());

        foreach (var group in assetsByType)
        {
            _sb.AppendLine($"^7Loaded {group.Key.Name} ({group.Count()}):^0");
            foreach (var (assetName, asset) in group)
            {
                _sb.Append($"{assetName,-60}");
                _sb.Append($"{asset.ToString()}\n");
            }
        }

        Shared.Console.Print(_sb.ToString());
    }
}