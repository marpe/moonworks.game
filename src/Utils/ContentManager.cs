using MoonWorks.Audio;
using MyGame.Fonts;
using MyGame.WorldsRoot;

namespace MyGame.Utils;

public class AsepriteAsset
{
    public AsepriteFile AsepriteFile;
    public TextureSlice TextureSlice;

    public AsepriteAsset(TextureSlice textureSlice, AsepriteFile aseprite)
    {
        TextureSlice = textureSlice;
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
    // private Dictionary<string, IDisposable> _disposableAssets = new();

    public ContentManager(MyGameMain game)
    {
        _game = game;
    }

    public T Load<T>(string assetName, bool forceReload = false)
    {
        if (forceReload)
        {
            _loadedAssets.Remove(assetName, out var oldAsset);
            // TODO (marpe): Check if old should be disposed
        }

        if (!_loadedAssets.TryGetValue(assetName, out var asset))
        {
            asset = InternalLoad<T>(assetName) ?? throw new Exception();
            _loadedAssets.Add(assetName, asset);
            Logs.LogInfo($"[{Shared.Game.Time.UpdateCount}] Loaded {typeof(T).Name} asset \"{assetName}\"");
        }

        if (typeof(T) == typeof(Texture) && asset is AsepriteAsset ase1)
        {
            asset = ase1.TextureSlice.Texture;
        }
        else if (typeof(T) == typeof(TextureSlice) && asset is AsepriteAsset ase2)
        {
            asset = ase2.TextureSlice;
        }

        if (typeof(T) == typeof(TextureSlice) && asset is Texture texture)
        {
            asset = new TextureSlice(texture);
        }
        else if (typeof(T) == typeof(Texture) && asset is TextureSlice slice)
        {
            asset = slice.Texture;
        }

        return (T)asset;
    }

    public void PackTextures()
    {
        Logs.LogInfo($"[{Shared.Game.Time.UpdateCount}] Starting atlas packing");
        var sw = Stopwatch.StartNew();
        var packer = new RectPacker(2048, 2048);

        var device = Shared.Content._game.GraphicsDevice;
        var atlasTexture = Texture.CreateTexture2D(device, 2048, 2048, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        var dstX = 0;
        var dstY = 0;

        var cb = device.AcquireCommandBuffer();

        var numPackedTextures = 0;
        var packedTextures = new List<string>();

        foreach (var (key, value) in _loadedAssets)
        {
            TextureSlice textureSlice;
            if (value is Texture texture)
                textureSlice = new TextureSlice(texture);
            else if (value is AsepriteAsset ase)
                textureSlice = ase.TextureSlice;
            else if (value is TextureSlice slice)
                textureSlice = slice;
            else
                continue;

            if (packer.AddRect(textureSlice.Rectangle.W, textureSlice.Rectangle.H, ref dstX, ref dstY))
            {
                var newSlice = new TextureSlice(atlasTexture, new Rect(dstX, dstY, textureSlice.Rectangle.W, textureSlice.Rectangle.H));
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
            var root = JsonConvert.DeserializeObject<RootJson>(json, JsonSerializerSettings) ?? throw new InvalidOperationException();
            return root;
        }

        if (t == typeof(Texture) ||
            t == typeof(TextureSlice))
        {
            return LoadTexture(assetName, Shared.Content._game.GraphicsDevice);
        }

        if (t == typeof(AsepriteAsset))
        {
            return LoadAseprite(assetName, Shared.Content._game.GraphicsDevice);
        }

        if (t == typeof(StaticSound))
        {
            return StaticSound.LoadWav(Shared.Content._game.AudioDevice, assetName);
        }

        if (t == typeof(BMFont))
        {
            return BMFont.LoadFromFile(Shared.Content._game.GraphicsDevice, assetName);
        }

        if (t == typeof(TTFFont))
        {
            var sizes = new[] { 18, 48 };
            return LoadAndAddTTFFonts(assetName, sizes);
        }

        throw new Exception($"Unsupported asset type \"{t.Name}\" ({assetName})");
    }

    private static TTFFont LoadAndAddTTFFonts(string fontPath, int[] sizes)
    {
        var device = Shared.Content._game.GraphicsDevice;

        var commandBuffer = device.AcquireCommandBuffer();

        var font = new Font(fontPath);
        var ttfFont = new TTFFont();

        foreach (var size in sizes)
        {
            var fontPacker = new Packer(device, font, size, 512, 512, 2u);
            fontPacker.PackFontRanges(TextBatcher.BasicLatin);
            fontPacker.SetTextureData(commandBuffer);
            var textBatchFont = new FontData(new TextBatch(device), fontPacker, font, size);
            ttfFont.Add(size, textBatchFont);
        }

        device.Submit(commandBuffer);

        foreach (var (_, fontData) in ttfFont.Sizes)
        {
            var pixels = TextureUtils.ConvertSingleChannelTextureToRGBA(device, fontData.Packer.Texture);
            TextureUtils.PremultiplyAlpha(pixels);
            var (width, height) = (fontData.Packer.Texture.Width, fontData.Packer.Texture.Height);
            var fontTexture = TextureUtils.CreateTexture(device, width, height, pixels);
            fontData.Texture = fontTexture;
        }

        return ttfFont;
    }

    private static AsepriteAsset LoadAseprite(string assetName, GraphicsDevice device)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var (texture, aseprite) = TextureUtils.LoadAseprite(device, ref commandBuffer, assetName);
        device.Submit(commandBuffer);
        return new AsepriteAsset(texture, aseprite);
    }

    private static Texture LoadTexture(string assetName, GraphicsDevice device)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.LoadPNG(device, commandBuffer, assetName);
        device.Submit(commandBuffer);
        return texture;
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
