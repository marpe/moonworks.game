using MyGame.Entities;

namespace MyGame;

public enum LoopType
{
    None,
    PingPong,
    Restart
}

public struct AnimationFrame
{
    public Vector2 Origin;
    public string TexturePath;
    public Rectangle SrcRect;
}

public class SpriteAnimation
{
    public readonly AnimationFrame[] Frames;
    public readonly float[] FrameDurations;
    public float TotalDuration;
    public LoopType LoopType;

    public SpriteAnimation(AnimationFrame[] frames, float[] frameDurations, LoopType loopType = LoopType.None)
    {
        Frames = frames;
        FrameDurations = frameDurations;
        TotalDuration = frameDurations.Sum();
        LoopType = loopType;
    }
}

[CustomInspector<GroupInspector>]
public class DrawComponent
{
    private Entity? _parent;
    private Entity Parent => _parent ?? throw new Exception();

    public Dictionary<string, SpriteAnimation> Animations = new(StringComparer.OrdinalIgnoreCase);

    [HideInInspector]
    public SpriteAnimation? CurrentAnimation;

    private SpriteAnimation? _previousAnimation;

    public Vector2 Squash = Vector2.One;

    [HideInInspector]
    public SpriteFlip Flip = SpriteFlip.None;

    public bool EnableSquash = true;

    public uint FrameIndex;

    public string TexturePath = "";

    private float _timer = 0;

    public bool IsAnimating = true;
    private Matrix3x2 _lastUpdateTransform = Matrix3x2.Identity;

    public void Initialize(Entity parent)
    {
        _parent = parent;

        if (TexturePath != "")
        {
            var aseAsset = Shared.Content.Load<AsepriteAsset>(TexturePath);
            var textureSlice = aseAsset.TextureSlice;
            var ase = aseAsset.AsepriteFile;
            var widthPerFrame = textureSlice.Rectangle.W / ase.Frames.Count;
            var frameHeight = textureSlice.Rectangle.H;
            Animations = CreateAnimations(ase, widthPerFrame, frameHeight, TexturePath);
            CurrentAnimation = Animations.FirstOrDefault().Value;
        }

        _lastUpdateTransform = GetTransform();
    }

    public void PlayAnimation(string animationName)
    {
        var nextAnimation = Animations[animationName];
        if (CurrentAnimation != nextAnimation)
        {
            _previousAnimation = CurrentAnimation;
            CurrentAnimation = nextAnimation;
            FrameIndex = 0;
            _timer = 0;
            IsAnimating = true;
        }
    }

    public void Update(float deltaSeconds)
    {
        if (CurrentAnimation == null)
            return;

        Squash = Vector2.SmoothStep(Squash, Vector2.One, deltaSeconds * 20f);

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
        var xform = Matrix3x2.Lerp(_lastUpdateTransform, GetTransform(), (float)alpha);
        var currentFrame = CurrentAnimation.Frames[FrameIndex];
        var texture = Shared.Content.Load<TextureAsset>(TexturePath).TextureSlice;
        var sprite = new Sprite(texture, currentFrame.SrcRect);
        renderer.DrawSprite(sprite, xform, Color.White, 0, Flip);
    }

    private Matrix3x2 GetTransform()
    {
        var spriteOrigin = Vector2.Zero;
        if (CurrentAnimation != null)
            spriteOrigin = CurrentAnimation.Frames[FrameIndex].Origin;

        if (spriteOrigin == Vector2.Zero)
            spriteOrigin = Parent.Pivot * World.DefaultGridSize;

        var origin = Parent.Size * Parent.Pivot;

        var squash = Matrix3x2.CreateTranslation(-origin) *
                     Matrix3x2.CreateScale(EnableSquash ? Squash : Vector2.One) *
                     Matrix3x2.CreateTranslation(origin);

        var position = Parent.Position.Current;

        var xform = Matrix3x2.CreateTranslation(origin - spriteOrigin) *
                    squash *
                    Matrix3x2.CreateTranslation(position);

        return xform;
    }

    #region Aseprite Loading

    private static Dictionary<string, SpriteAnimation> CreateAnimations(AsepriteFile ase, int widthPerFrame, int frameHeight, string texturePath)
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
            var frames = new AnimationFrame[ase.Frames.Count];
            var durations = new float[ase.Frames.Count];
            durations.AsSpan().Fill(1f / 12f);

            for (var i = 0; i < ase.Frames.Count; i++)
            {
                var sourceRect = new Rectangle((int)(i * widthPerFrame), 0, (int)widthPerFrame, (int)frameHeight);
                frames[i] = new AnimationFrame
                {
                    TexturePath = texturePath,
                    SrcRect = sourceRect
                };
            }

            animations.Add("Default", new SpriteAnimation(frames, durations));
            return animations;
        }

        Vector2? pivot = null;
        foreach (var tag in tags)
        {
            var numberOfFrames = tag.FrameTo - tag.FrameFrom + 1;
            var frames = new AnimationFrame[numberOfFrames];
            var durations = new float[numberOfFrames];
            int j = 0;
            for (int i = tag.FrameFrom; i <= tag.FrameTo; i++, j++)
            {
                frames[j] = new AnimationFrame()
                {
                    TexturePath = texturePath,
                    SrcRect = srcRects[i],
                };
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

            if (pivot != null)
            {
                for (var n = 0; n < frames.Length; n++)
                {
                    frames[n].Origin = pivot.Value;
                }
            }

            animations.Add(tag.TagName, new SpriteAnimation(frames, durations, loopType));
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

    public void SetLastUpdateTransform()
    {
        _lastUpdateTransform = GetTransform();
    }
}
