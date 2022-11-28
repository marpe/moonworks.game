namespace MyGame.Utils;

public class ContentManager
{
    private GraphicsDevice _device;
    public Dictionary<string, Texture> LoadedTextures = new();

    public ContentManager(GraphicsDevice device)
    {
        _device = device;
    }
    
    public void AddTexture(string path, Texture asset)
    {
        LoadedTextures[path] = asset;
    }

    public void LoadTextures(List<string> texturePaths)
    {
        var texturesPendingSubmit = new Dictionary<string, Texture>(); 
        var commandBuffer = _device.AcquireCommandBuffer();
        foreach (var texturePath in texturePaths)
        {
            var extension = Path.GetExtension(texturePath);
            if (extension == ".aseprite")
            {
                texturesPendingSubmit.Add(texturePath, TextureUtils.LoadAseprite(_device, commandBuffer, texturePath));
            }
            else if(extension == ".png")
            {
                AddTexture(texturePath, Texture.LoadPNG(_device, commandBuffer, texturePath));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported extension: {extension}, path: {texturePath}");
            }
        }

        _device.Submit(commandBuffer);
        
        foreach (var (kvp, texture) in texturesPendingSubmit)
        {
            AddTexture(kvp, texture);
        }
    }
    
    public Texture this[string path] => LoadedTextures[path];
}
