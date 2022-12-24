namespace MyGame;

public enum LoopType
{
    None,
    PingPong,
    Restart
}

public class SpriteAnimation
{
    public readonly Sprite[] Frames;
    public readonly float[] FrameDurations;
    public float TotalDuration;
    public LoopType LoopType;

    public SpriteAnimation(Sprite[] frames, float[] frameDurations, LoopType loopType = LoopType.None)
    {
        Frames = frames;
        FrameDurations = frameDurations;
        TotalDuration = frameDurations.Sum();
        LoopType = loopType;
    }
}

public class DrawComponent
{
    private Entity? _parent;
    private Entity Parent => _parent ?? throw new Exception();

    public Dictionary<string, SpriteAnimation> Animations = new(StringComparer.OrdinalIgnoreCase);
    public SpriteAnimation? CurrentAnimation;

    public Vector2 Squash = Vector2.One;

    [HideInInspector] public SpriteFlip Flip = SpriteFlip.None;

    public bool EnableSquash = true;

    public uint FrameIndex;

    public string TexturePath = "";

    private float _timer = 0;

    public bool IsAnimating = true;

    public void Initialize(Entity parent)
    {
        _parent = parent;

        if (TexturePath != "")
        {
            Shared.Content.LoadAndAddTextures(new[] { TexturePath });
            var (texture, ase) = Shared.Content.GetAseprite(TexturePath);
            Animations = CreateAnimations(ase, texture);
            CurrentAnimation = Animations.FirstOrDefault().Value;
        }
    }

    public void Update(float deltaSeconds)
    {
        if (CurrentAnimation == null)
            return;
        if (!IsAnimating)
            return;

        _timer += deltaSeconds;
        if (_timer > CurrentAnimation.FrameDurations[FrameIndex])
        {
            _timer -= CurrentAnimation.FrameDurations[FrameIndex];
            FrameIndex++;
            if (FrameIndex > CurrentAnimation.Frames.Length - 1)
                FrameIndex = 0;
        }
    }

    public void Draw(Renderer renderer, double alpha)
    {
        if (CurrentAnimation == null)
            return;
        var xform = GetTransform(alpha);
        renderer.DrawSprite(CurrentAnimation.Frames[FrameIndex], xform, Color.White, 0, Flip);

        // bullet
        /*var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        var srcRect = new Rectangle(4 * 16, 0, 16, 16);
        var xform = bullet.GetTransform(alpha);
        renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, bullet.Flip);*/

        // Enemy draw
        /*var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        var offset = entity.EntityType switch
        {
            EntityType.Slug => 5,
            EntityType.BlueBee => 3,
            _ => 1,
        };

        var frameIndex = (int)(entity.TotalTimeActive * 10) % 2;
        var srcRect = new Rectangle(offset * 16 + frameIndex * 16, 16, 16, 16);
        var xform = entity.GetTransform(alpha);
        renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, entity.Flip);*/
    }

    private Matrix4x4 GetTransform(double alpha)
    {
        var squash = Matrix3x2.CreateTranslation(-Parent.Size * Parent.Pivot) *
                     Matrix3x2.CreateScale(EnableSquash ? Squash : Vector2.One) *
                     Matrix3x2.CreateTranslation(Parent.Size * Parent.Pivot);

        var xform = Matrix3x2.CreateTranslation(Parent.Pivot * (Parent.Size - World.DefaultGridSize)) *
                    squash *
                    Matrix3x2.CreateTranslation(Vector2.Lerp(Parent.Position.LastUpdatePosition, Parent.Position.Current, (float)alpha));

        return xform.ToMatrix4x4();
    }

    #region Aseprite Loading

    private static Dictionary<string, SpriteAnimation> CreateAnimations(AsepriteFile ase, Texture texture)
    {
        var tags = new List<AsepriteFile.FrameTag>();
        var animations = new Dictionary<string, SpriteAnimation>();
        foreach (var frame in ase.Frames)
        {
            tags.AddRange(frame.FrameTags);
        }

        var srcRects = new List<Rectangle>();
        for (var i = 0; i < ase.Frames.Count; i++)
        {
            var spriteRect = new Rectangle(
                i * ase.Header.Width,
                0,
                ase.Header.Width,
                ase.Header.Height
            );
            srcRects.Add(spriteRect);
        }
        
        if (tags.Count == 0)
        {
            var widthPerFrame = texture.Width / ase.Frames.Count;
            var sprites = new Sprite[ase.Frames.Count];
            var durations = new float[ase.Frames.Count];
            durations.AsSpan().Fill(1f / 12f);
            
            for (var i = 0; i < ase.Frames.Count; i++)
            {
                var sourceRect = new Rectangle((int)(i * widthPerFrame), 0, (int)widthPerFrame, (int)texture.Height);
                sprites[i] = new Sprite(texture, sourceRect);
            }

            animations.Add("Default", new SpriteAnimation(sprites, durations));
            return animations;
        }

        Vector2? pivot = null;
        foreach (var tag in tags)
        {
            var numberOfFrames = tag.FrameTo - tag.FrameFrom + 1;
            var sprites = new Sprite[numberOfFrames];
            var durations = new float[numberOfFrames];
            int j = 0;
            for (int i = tag.FrameFrom; i <= tag.FrameTo; i++, j++)
            {
                sprites[j] = new Sprite(texture, srcRects[i]);
                durations[j] = ase.Frames[i].FrameDuration / 1000f;
            }

            var loopType = tag.Animation == AsepriteFile.LoopAnimation.PingPong ? LoopType.PingPong : LoopType.Restart;

            if (tag.UserData != null)
            {
                var (newPivot, newLoopType) = ParseUserData(tag.UserData.Text);
                if (newPivot != null)
                {
                    pivot = newPivot;
                }

                if (newLoopType.HasValue)
                {
                    loopType = newLoopType.Value;
                }
            }

            // TODO (marpe): Add somewhere
            /*if (pivot != null)
            {
                for (var n = 0; n < sprites.Length; n++)
                {
                    sprites[n].Origin = pivot.Value;
                }
            }*/

            animations.Add(tag.TagName, new SpriteAnimation(sprites, durations, loopType));
        }

        return animations;
    }

    private static (Vector2? pivot, LoopType? loopType) ParseUserData(ReadOnlySpan<char> userText)
    {
        LoopType? loopType = null;
        Vector2? pivot = null;
        var lastProp = 0;

        for (var i = 0; i < userText.Length; i++)
        {
            if (userText[i] == ':')
            {
                var prop = userText.Slice(lastProp, i - lastProp).Trim();
                var valueSpan = userText.Slice(i + 1);

                for (var k = 0; k < valueSpan.Length; k++)
                {
                    if (valueSpan[k] == ';')
                    {
                        valueSpan = valueSpan.Slice(0, k);
                        break;
                    }
                }

                var trimmedValue = valueSpan.Trim();

                if (prop.StartsWith("pivot"))
                {
                    pivot = ConsoleUtils.ParsePoint(trimmedValue).ToVec2();
                }
                else if (prop.StartsWith("loop"))
                {
                    if (trimmedValue.StartsWith("none"))
                    {
                        loopType = LoopType.None;
                    }
                }

                i += valueSpan.Length + 1;
                lastProp = i + 1;
            }
        }

        return (pivot, loopType);
    }

    #endregion
}
