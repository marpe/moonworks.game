using MoonWorks.Graphics.Font;
using MyGame.Graphics;
using MyGame.TWImGui;
using MyGame.Utils;
using WellspringCS;
using AlignH = WellspringCS.Wellspring.HorizontalAlignment;
using AlignV = WellspringCS.Wellspring.VerticalAlignment;

namespace MyGame;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct FancyTextPart
{
    public string Character;
    public Color Color;
    public int Index;
    public bool IsWaving;
    public bool IsShaking;
    public float Alpha;
    public Point Offset;
    public float WaveAmplitude;
    public float WaveSpeed;
    public float WaveOffset;
    public float Rotation;
    public Vector2 Scale;

    public FancyTextPart(int index, char character, Color color, bool isWaving, bool isShaking, Point offset)
    {
        Index = index;
        Character = new string(character, 1);
        Color = color;
        IsWaving = isWaving;
        IsShaking = isShaking;
        Offset = offset;

        WaveAmplitude = 4;
        WaveSpeed = 8f;
        WaveOffset = .25f;
        Alpha = 1;
        Rotation = 0;
        Scale = Vector2.One;
    }

    public string DebugDisplayString => Character;
}

public class FancyTextComponent
{
    [Inspectable] public readonly FancyTextPart[] Parts = Array.Empty<FancyTextPart>();

    [Range(0f, 1f, .01f)] public float Alpha = 1f;
    public AlignH AlignH { get; set; } = AlignH.Center;
    public AlignV AlignV { get; set; } = AlignV.Middle;

    public static float ShakeSpeed = 100f;
    public static float ShakeAmount = 1f;

    public float Timer;

    private readonly char[] _strippedText;
    
    public float Rotation;
    public Vector2 Position;
    public Vector2 Scale = Vector2.One;

    public FancyTextComponent(ReadOnlySpan<char> rawText)
    {
        var (stripped, parts) = ParseText(rawText);
        _strippedText = stripped;
        Parts = parts;
    }

    public static Vector2 GetAlignVector(AlignH horizontal, AlignV vertical)
    {
        var originX = horizontal switch
        {
            AlignH.Left => 0,
            AlignH.Center => .5f,
            AlignH.Right => 1,
            _ => 0
        };

        var originY = vertical switch
        {
            AlignV.Top => 0,
            AlignV.Middle => .5f,
            AlignV.Bottom => 1f,
            _ => 0
        };

        return new Vector2(originX, originY);
    }

    /// <summary>
    /// <~>Wavy</~>
    /// <#rrggbb>Colored</#>
    /// <*>Shaking</*>
    /// </summary>
    private static (char[], FancyTextPart[]) ParseText(ReadOnlySpan<char> text)
    {
        var parts = new List<FancyTextPart>();

        var currentColor = Color.White;
        Stack<Color> colorStack = new();
        var isInTag = false;
        var characterIndex = 0;
        var isWaving = false;
        var isShaking = false;
        var sb = new StringBuilder();

        var offset = new Point();

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                isInTag = true;

                if (i < text.Length - 1)
                {
                    // text has at least 1 more character
                    if (text[i + 1] == '#')
                    {
                        // try parse color and push to stack
                        if (i + 6 < text.Length - 1)
                        {
                            var hex = text.Slice(i + 2, 6);
                            colorStack.Push(currentColor);
                            currentColor = ColorExt.FromHex(hex);
                        }
                    }
                    else if (text[i + 1] == '~')
                    {
                        isWaving = true;
                    }
                    else if (text[i + 1] == '*')
                    {
                        isShaking = true;
                    }
                    // text has at least 2 characters more
                    else if (i + 1 < text.Length - 1)
                    {
                        // check for terminating color
                        if (text[i + 1] == '/' && text[i + 2] == '#')
                        {
                            currentColor = colorStack.Count <= 0 ? Color.White : colorStack.Pop();
                        }
                        else if (text[i + 1] == '/' && text[i + 2] == '~')
                        {
                            isWaving = false;
                        }
                        else if (text[i + 1] == '/' && text[i + 2] == '*')
                        {
                            isShaking = false;
                        }
                    }
                }
            }
            else if (text[i] == '>')
            {
                isInTag = false;
            }
            else if (text[i] == '\r' || text[i] == '\n')
            {
                if (i < text.Length - 1 && text[i] == '\r' && text[i + 1] == '\n')
                {
                    continue; // ignore \r when it's followed by \n
                }

                sb.Append(text[i]);
                offset.X = 0;
                offset.Y++;
                characterIndex++;
            }
            else if (!isInTag)
            {
                sb.Append(text[i]);
                if (text[i] != ' ')
                {
                    parts.Add(new FancyTextPart(
                            characterIndex,
                            text[i],
                            currentColor,
                            isWaving,
                            isShaking,
                            offset
                        )
                    );
                }

                offset.X++;
                characterIndex++;
            }
        }

        var str = sb.ToString().ToCharArray();
        return (str, parts.ToArray());
    }

    private static Vector2 GetShakeOffset(float shakeFreqOffset, float shakeSpeed, float shakeAmount, float t)
    {
        var a = shakeSpeed * t + shakeFreqOffset;
        return new Vector2(MathF.Cos(a), MathF.Sin(a)) * shakeAmount;
    }

    private static Vector2 GetWaveOffset(FancyTextPart part, float t)
    {
        return new Vector2(0, MathF.Sin(part.WaveSpeed * t + part.Index * part.WaveOffset) * part.WaveAmplitude);
    }

    public void Update(float deltaSeconds)
    {
        Timer += deltaSeconds;
    }

    public void Render(BMFontType fontType, Renderer renderer, double alpha)
    {
        var font = renderer.GetFont(fontType);
        var textSize = font.MeasureString(_strippedText);
        var origin = GetAlignVector(AlignH, AlignV) * textSize;

        var rotation = Matrix3x2.CreateRotation(Rotation);
        var offset = Vector2.Zero;

        var charSize = font.MeasureString(' ', 'X');

        for (var i = 0; i < Parts.Length; i++)
        {
            var part = Parts[i];
            var waveOffset = part.IsWaving ? GetWaveOffset(part, Timer) : Vector2.Zero;
            var shakeOffset = part.IsShaking ? GetShakeOffset(0, ShakeSpeed, ShakeAmount, Timer) : Vector2.Zero;
            var color = MultiplyAlpha(part.Color, Alpha, part.Alpha);

            var partOffset = part.Offset * charSize;
            var partPos = - origin + shakeOffset + waveOffset + partOffset;
            var partOrigin = charSize / 2;
            var finalPos = partOrigin + Position + Vector2.Transform(partPos * Scale, rotation);

            renderer.DrawBMText(fontType, part.Character, finalPos, partOrigin, Scale * part.Scale, Rotation + part.Rotation, 0, color);
            // renderer.DrawPoint(finalPos, Color.Blue, 4f);
        }
    }

    private static int CountLines(ReadOnlySpan<char> text)
    {
        int numLines = 0;
        for(var i = 0; i < text.Length; i++)
            if (text[i] == '\n')
                numLines++;
        return numLines;
    }

    private static Color MultiplyAlpha(Color color, float partAlpha, float mainAlpha)
    {
        return color * (partAlpha * mainAlpha); 
        var a = Convert.ToByte(mainAlpha * partAlpha * 255);
        var r = (byte)(color.R * color.A / 255f);
        var g = (byte)(color.G * color.A / 255f);
        var b = (byte)(color.B * color.A / 255f);
        return new Color(r, g, b, a);
    }

    private static Vector2 MeasureText(ReadOnlySpan<char> text, FontData font, HorizontalAlignment alignH, VerticalAlignment alignV)
    {
        var s = new string(text);
        font.Packer.TextBounds(s, 500, 500, alignH, alignV, out var rect);
        return new Vector2(rect.W, rect.H);
    }

    /*private static void UpdateBounds(BMFont font, List<FancyTextPart> parts, AlignH alignH, AlignV alignV, out Vector2 bounds, out Vector2 origin)
    {
        bounds = Vector2.Zero;
        var previousChar = ' ';
        foreach (var p in parts)
        {
            var size = font.MeasureString(previousChar, p.Character);
            bounds.X += size.X;
            if (p.Character == '\n')
                bounds.Y = font.Font.LineHeight;
            previousChar = p.Character;
        }
        origin = GetAlignVector(alignH, alignV) * bounds;
    }*/
}