using MoonWorks.Audio;

namespace MyGame.Utils;

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

    private Dictionary<string, Texture> _loadedTextures = new();
    private Dictionary<string, BMFont> _loadedBMFonts = new();
    private Dictionary<string, StaticSound> _loadedSound = new();
    private Dictionary<string, TTFFont> _loadedTTFFonts = new();
    private Dictionary<string, WorldsRoot.RootJson> _loadedRoots = new();

    public ContentManager(MyGameMain game)
    {
        _game = game;
    }

    public void AddTexture(string path, Texture asset)
    {
        _loadedTextures.Add(path, asset);
    }

    public void ReplaceTexture(string path, Texture texture)
    {
        if (_loadedTextures.TryGetValue(path, out var oldTexture))
        {
            oldTexture.Dispose();
            _loadedTextures.Remove(path);
        }

        AddTexture(path, texture);
    }

    public WorldsRoot.RootJson LoadRoot(string path, bool forceReload)
    {
        if (!forceReload && _loadedRoots.TryGetValue(path, out var root))
            return root;

        var json = File.ReadAllText(path);
        var newRoot = JsonConvert.DeserializeObject<WorldsRoot.RootJson>(json, JsonSerializerSettings) ?? throw new InvalidOperationException();
        _loadedRoots[path] = newRoot;
        return newRoot;
    }

    public BMFont LoadAndAddBMFont(string path)
    {
        var font = BMFont.LoadFromFile(_game.GraphicsDevice, path);
        _loadedBMFonts.Add(path, font);
        return font;
    }

    public StaticSound LoadAndAddSound(string path)
    {
        var wav = StaticSound.LoadWav(_game.AudioDevice, path);
        _loadedSound.Add(path, wav);
        return wav;
    }

    private void AddTTFFont(string path, TTFFont font)
    {
        _loadedTTFFonts.Add(path, font);
    }

    public TTFFont GetTTFFont(string path)
    {
        return _loadedTTFFonts[path];
    }

    public bool HasTexture(string path)
    {
        return _loadedTextures.ContainsKey(path);
    }

    public Texture GetTexture(string path)
    {
        return _loadedTextures[path];
    }

    public void LoadAndAddTTFFonts((string, int[])[] fontPaths)
    {
        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        var loadedFonts = new Dictionary<string, TTFFont>();

        foreach (var (path, sizes) in fontPaths)
        {
            var font = new Font(path);
            var ttfFont = new TTFFont();

            foreach (var size in sizes)
            {
                var fontPacker = new Packer(_game.GraphicsDevice, font, size, 512, 512, 2u);
                fontPacker.PackFontRanges(TextBatcher.BasicLatin);
                fontPacker.SetTextureData(commandBuffer);
                var textBatchFont = new FontData(new TextBatch(_game.GraphicsDevice), fontPacker, font, size);
                ttfFont.Add(size, textBatchFont);
            }

            loadedFonts.Add(path, ttfFont);
        }

        _game.GraphicsDevice.Submit(commandBuffer);

        foreach (var (path, ttfFont) in loadedFonts)
        {
            foreach (var (size, fontData) in ttfFont.Sizes)
            {
                var pixels = TextureUtils.ConvertSingleChannelTextureToRGBA(_game.GraphicsDevice, fontData.Packer.Texture);
                TextureUtils.PremultiplyAlpha(pixels);
                var (width, height) = (fontData.Packer.Texture.Width, fontData.Packer.Texture.Height);
                var fontTexture = TextureUtils.CreateTexture(_game.GraphicsDevice, width, height, pixels);
                fontData.Texture = fontTexture;
            }

            AddTTFFont(path, ttfFont);
        }
    }

    public void LoadAndAddTextures(IEnumerable<string> texturePaths)
    {
        var texturesPendingSubmit = new Dictionary<string, Texture>();
        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        foreach (var texturePath in texturePaths)
        {
            if (_loadedTextures.ContainsKey(texturePath) || texturesPendingSubmit.ContainsKey(texturePath))
                continue;

            var extension = Path.GetExtension(texturePath);
            if (extension == ".aseprite")
            {
                var asepriteTexture = TextureUtils.LoadAseprite(_game.GraphicsDevice, ref commandBuffer, texturePath);
                texturesPendingSubmit.Add(texturePath, asepriteTexture);
            }
            else if (extension == ".png")
            {
                var pngTexture = Texture.LoadPNG(_game.GraphicsDevice, commandBuffer, texturePath);
                AddTexture(texturePath, pngTexture);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported extension: {extension}, path: {texturePath}");
            }
        }

        _game.GraphicsDevice.Submit(commandBuffer);

        foreach (var (kvp, texture) in texturesPendingSubmit)
        {
            AddTexture(kvp, texture);
        }
    }

    [ConsoleHandler("content_list")]
    public static void ListLoadedAssets()
    {
        _sb.Clear();

        _sb.AppendLine($"^7Loaded textures ({Shared.Content._loadedTextures.Count}):^0");
        foreach (var (path, texture) in Shared.Content._loadedTextures)
        {
            _sb.Append($"{path,-40}");
            _sb.Append($"{texture.Width}x{texture.Height}\n");
        }

        _sb.AppendLine($"^7Loaded BMFonts ({Shared.Content._loadedBMFonts.Count}):^0");
        foreach (var (path, bmFont) in Shared.Content._loadedBMFonts)
        {
            _sb.Append($"{path,-40}");
            _sb.Append($"{bmFont.Font.FamilyName,-20}\n");
        }

        _sb.AppendLine($"^7Loaded TTFFonts ({Shared.Content._loadedTTFFonts.Count}):^0");
        foreach (var (path, font) in Shared.Content._loadedTTFFonts)
        {
            _sb.Append($"{path,-40}");
            _sb.Append($"Sizes: {string.Join(", ", font.Sizes.Keys),-20}\n");
        }

        _sb.AppendLine($"^7Loaded Sound ({Shared.Content._loadedSound.Count}):^0");
        foreach (var (path, _) in Shared.Content._loadedSound)
        {
            _sb.Append($"{path,-40}\n");
        }

        Shared.Console.Print(_sb.ToString());
    }
}
