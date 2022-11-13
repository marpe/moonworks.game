using System.Xml;
using MyGame.Utils;

namespace MyGame.BitmapFonts;

public class BitmapFont
{
    public static BitmapFont LoadXml(TextReader reader)
    {
        var document = new XmlDocument();
        var font = new BitmapFont();
        var pageData = new SortedDictionary<int, Page>();
        var kerningDictionary = new Dictionary<Kerning, int>();
        var charDictionary = new Dictionary<char, Character>();

        document.Load(reader);
        var root = document.DocumentElement ?? throw new InvalidOperationException();

        // load the basic attributes
        var properties = root.SelectSingleNode("info") ?? throw new InvalidOperationException();
        font.FamilyName = properties.AttributeValue("face");
        font.FontSize = properties.ParseInt("size");
        font.Bold = properties.ParseBool("bold", false);
        font.Italic = properties.ParseBool("italic", false);
        font.Unicode = properties.ParseBool("unicode", false);
        font.StretchedHeight = properties.ParseInt("stretchH");
        font.Charset = properties.AttributeValue("charset");
        font.Smoothed = properties.ParseBool("smooth", false);
        font.SuperSampling = properties.ParseInt("aa");
        font.Padding = ParsePadding(properties.AttributeValue("padding"));
        font.Spacing = ParseInt2(properties.AttributeValue("spacing"));
        font.OutlineSize = properties.ParseInt("outline");

        // common attributes
        properties = root.SelectSingleNode("common") ?? throw new InvalidOperationException();
        font.BaseHeight = properties.ParseInt("base");
        font.LineHeight = properties.ParseInt("lineHeight");
        font.TextureSize = new Point(properties.ParseInt("scaleW"), properties.ParseInt("scaleH"));
        font.Packed = properties.ParseBool("packed", false);

        font.AlphaChannel = properties.ParseInt("alphaChnl", 0);
        font.RedChannel = properties.ParseInt("redChnl", 0);
        font.GreenChannel = properties.ParseInt("greenChnl", 0);
        font.BlueChannel = properties.ParseInt("blueChnl", 0);

        // load texture information
        var pageNodes = root.SelectNodes("pages/page");
        if (pageNodes != null)
        {
            foreach (XmlNode node in pageNodes)
            {
                var page = new Page();
                page.Id = node.ParseInt("id");
                page.Filename = node.AttributeValue("file");

                pageData.Add(page.Id, page);
            }
        }

        font.Pages = pageData.Values.ToArray();

        // load character information
        var charNodes = root.SelectNodes("chars/char");
        if (charNodes != null)
        {
            foreach (XmlNode node in charNodes)
            {
                var character = new Character
                {
                    Char = (char)node.ParseInt("id"),
                    Bounds = new Rectangle(
                        node.ParseInt("x"),
                        node.ParseInt("y"),
                        node.ParseInt("width"),
                        node.ParseInt("height")
                    ),
                    Offset = new Point(
                        node.ParseInt("xoffset"),
                        node.ParseInt("yoffset")
                    ),
                    XAdvance = node.ParseInt("xadvance"),
                    TexturePage = node.ParseInt("page"),
                    Channel = node.ParseInt("chnl"),
                };

                charDictionary[character.Char] = character;
            }
        }

        font.Characters = charDictionary;
        font.DefaultCharacter = font.Characters['?'];

        // loading kerning information
        var kerningNodes = root.SelectNodes("kernings/kerning");
        if (kerningNodes != null)
        {
            foreach (XmlNode node in kerningNodes)
            {
                var key = new Kerning(
                    (char)node.ParseInt("first"),
                    (char)node.ParseInt("second"),
                    node.ParseInt("amount")
                );

                if (!kerningDictionary.ContainsKey(key))
                {
                    kerningDictionary.Add(key, key.Amount);
                }
            }
        }

        font.Kernings = kerningDictionary;

        return font;
    }

    private static Point ParseInt2(string s)
    {
        var parts = s.Split(',');
        return new Point
        {
            X = Convert.ToInt32(parts[0].Trim()),
            Y = Convert.ToInt32(parts[1].Trim()),
        };
    }

    private static Padding ParsePadding(string s)
    {
        var parts = s.Split(',');
        return new Padding()
        {
            Left = Convert.ToInt32(parts[3].Trim()),
            Top = Convert.ToInt32(parts[0].Trim()),
            Right = Convert.ToInt32(parts[1].Trim()),
            Bottom = Convert.ToInt32(parts[2].Trim()),
        };
    }

    #region Properties

    /// <summary>alpha channel.</summary>
    /// <remarks>
    /// Set to 0 if the channel holds the glyph data, 1 if it holds the outline, 2 if it holds the glyph and the outline, 3 if it's set to zero, and 4 if it's
    /// set to one.
    /// </remarks>
    public int AlphaChannel;

    /// <summary>number of pixels from the absolute top of the line to the base of the characters.</summary>
    public int BaseHeight;

    /// <summary>blue channel.</summary>
    /// <remarks>
    /// Set to 0 if the channel holds the glyph data, 1 if it holds the outline, 2 if it holds the glyph and the outline, 3 if it's set to zero, and 4 if it's
    /// set to one.
    /// </remarks>
    public int BlueChannel;

    public bool Bold;

    /// <summary>characters that comprise the font.</summary>
    public IDictionary<char, Character> Characters = new Dictionary<char, Character>();

    /// <summary>name of the OEM charset used.</summary>
    public string Charset = string.Empty;

    /// <summary>name of the true type font.</summary>
    public string FamilyName = string.Empty;

    /// <summary>size of the font.</summary>
    public int FontSize;

    /// <summary>green channel.</summary>
    /// <remarks>
    /// Set to 0 if the channel holds the glyph data, 1 if it holds the outline, 2 if it holds the glyph and the outline, 3 if it's set to zero, and 4 if it's
    /// set to one.
    /// </remarks>
    public int GreenChannel;

    public bool Italic;

    /// <summary>character kernings for the font.</summary>
    public IDictionary<Kerning, int> Kernings = new Dictionary<Kerning, int>();

    /// <summary>distance in pixels between each line of text.</summary>
    public int LineHeight;

    /// <summary>outline thickness for the characters.</summary>
    public int OutlineSize;

    /// <summary>Gets or sets a value indicating whether the monochrome charaters have been packed into each of the texture channels.</summary>
    /// <remarks>When packed, the <see cref="AlphaChannel" /> property describes what is stored in each channel.</remarks>
    public bool Packed;

    /// <summary>padding for each character.</summary>
    public Padding Padding;

    /// <summary>texture pages for the font.</summary>
    public Page[] Pages = Array.Empty<Page>();

    /// <summary>red channel.</summary>
    /// <remarks>
    /// Set to 0 if the channel holds the glyph data, 1 if it holds the outline, 2 if it holds the glyph and the outline, 3 if it's set to zero, and 4 if it's
    /// set to one.
    /// </remarks>
    public int RedChannel;

    /// <summary>Gets or sets a value indicating whether the font is smoothed.</summary>
    public bool Smoothed;

    /// <summary>spacing for each character.</summary>
    public Point Spacing;

    /// <summary>font height stretch.</summary>
    /// <remarks>100% means no stretch.</remarks>
    public int StretchedHeight;

    /// <summary>level of super sampling used by the font.</summary>
    /// <remarks>A value of 1 indicates no super sampling is in use.</remarks>
    public int SuperSampling;

    /// <summary>size of the texture images used by the font.</summary>
    public Point TextureSize;

    public bool Unicode;

    public Character DefaultCharacter = new();

    #endregion
}
