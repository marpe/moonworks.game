namespace MyGame.Aseprite;

public static class AsepriteToTextureAtlasConverter
{
    private static AsepriteFile.Layer? GetParentLayer(AsepriteFile.AsepriteFrame frame, AsepriteFile.Layer layer)
    {
        if (layer.ChildLevel == 0)
        {
            return null;
        }

        var layers = frame.Layers;
        var index = layers.IndexOf(layer);

        if (index < 0)
        {
            return null;
        }

        for (var i = index - 1; i > 0; i--)
        {
            if (layers[i].ChildLevel == layer.ChildLevel - 1)
            {
                return layers[i];
            }
        }

        return null;
    }

    private static void GetTextureFromCel(Span<uint> result, int frameIndex, AsepriteFile asepriteFile, AsepriteFile.Cel cel,
        AsepriteFile.LayerBlendMode blendMode, byte opacity)
    {
        var canvasWidth = asepriteFile.Header.Width;
        var canvasHeight = asepriteFile.Header.Height;

        var atlasWidth = asepriteFile.Header.Width * asepriteFile.Frames.Count;
        var atlasHeight = asepriteFile.Header.Height;

        // skip data in file which is outside the canvas by clamping the values
        var y0 = Math.Clamp(cel.YPosition, 0, canvasHeight);
        var y1 = Math.Clamp(cel.YPosition + cel.Height, 0, canvasHeight);
        var x0 = Math.Clamp(cel.XPosition, 0, canvasWidth);
        var x1 = Math.Clamp(cel.XPosition + cel.Width, 0, canvasWidth);

        var startIndexInColorArr = frameIndex * canvasWidth + y0 * atlasWidth + x0;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var ys = y - y0;
                var xs = x - x0;
                var pixelIndex = cel.Width * ys + xs;
                var celPixel = cel.Pixels[pixelIndex];
                var colorIndex = startIndexInColorArr + atlasWidth * ys + xs;

                result[colorIndex] = blendMode switch
                {
                    AsepriteFile.LayerBlendMode.Normal => Texture2DBlender.BlendNormal(result[colorIndex], celPixel, opacity),
                    AsepriteFile.LayerBlendMode.Multiply => Texture2DBlender.BlendMultiply(result[colorIndex], celPixel, opacity),
                    _ => throw new NotImplementedException(),
                };
            }
        }
    }

    private static void GetFrame(AsepriteFile aseprite, int frameIndex, Span<uint> result)
    {
        var frame = aseprite.Frames[frameIndex];
        var layers = aseprite.Frames[0].Layers;
        var cels = frame.Cels;

        cels.Sort((ca, cb) => ca.LayerIndex.CompareTo(cb.LayerIndex));

        for (var i = 0; i < cels.Count; i++)
        {
            var layer = layers[cels[i].LayerIndex];
            if (layer.LayerName.StartsWith("@")) // ignore metadata layer
            {
                continue;
            }

            var blendMode = (AsepriteFile.LayerBlendMode)layer.BlendMode;
            var opacity = Math.Min(layer.Opacity, cels[i].Opacity);

            var visibility = layer.Visible;

            var parent = GetParentLayer(frame, layer);
            while (parent != null)
            {
                visibility &= parent.Visible;
                if (visibility == false)
                {
                    break;
                }

                parent = GetParentLayer(frame, parent);
            }

            if (visibility == false || (AsepriteFile.LayerType)layer.Type == AsepriteFile.LayerType.Group)
            {
                continue;
            }

            GetTextureFromCel(result, frameIndex, aseprite, cels[i], blendMode, opacity);
        }
    }

    public static (uint[] data, List<Rectangle> rects) GetTextureData(AsepriteFile aseprite)
    {
        var atlasWidth = aseprite.Header.Width * aseprite.Frames.Count;
        var atlasHeight = aseprite.Header.Height;

        var atlas = new uint[atlasWidth * atlasHeight];
        List<Rectangle> spriteRects = new();

        for (var i = 0; i < aseprite.Frames.Count; i++)
        {
            var spriteRect = new Rectangle(
                i * aseprite.Header.Width,
                0,
                aseprite.Header.Width,
                atlasHeight
            );

            GetFrame(aseprite, i, atlas);
            spriteRects.Add(spriteRect);
        }

        return (atlas, spriteRects);
    }
}
