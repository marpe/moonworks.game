using System.Runtime.Serialization;

namespace MyGame.WorldsRoot;

public static class IdGen
{
    private static int _idCounter;
    public static int NewId => _idCounter++;
}

public class WorldsRoot
{
    public string Version = "0.0.1";

    public int DefaultGridSize = 16;
    public List<NewWorld> Worlds = new();
    public List<EntityDefinition> EntityDefinitions = new();
    public List<FieldDef> LevelFieldDefinitions = new();
    public List<LayerDef> LayerDefinitions = new();
    public List<TileSetDef> TileSetDefinitions = new();

    public WorldsRoot()
    {
    }
}

public class TileSetDef
{
    public int Uid = IdGen.NewId;
    public string Identifier = "TileSet";
    public string Path = "";
}

public enum LayerType
{
    IntGrid,
    Entities,
    Tiles,
    AutoLayer
}

public class LayerDef
{
    public int Uid = IdGen.NewId;
    public LayerType LayerType = LayerType.IntGrid;
    public string Identifier = "Layer";
    public List<string> RequiredTags = new();
    public List<string> ExcludedTags = new();
    public uint GridSize = 16;
    public List<IntGridValue> IntGridValues = new();

    public LayerDef()
    {
    }
}

public class IntGridValue
{
    public int Value;
    public string Identifier = "Value";
    public Color Color = Color.White;
}

public enum FieldType
{
    Int,
    Float,
    String,
    Bool,
    Color,
    Point,
    Vector2,
}

public class FieldDef
{
    public int Uid = IdGen.NewId;
    public string Identifier = "Field";
    public FieldType FieldType;
    public object? DefaultValue = null;
    public bool IsArray;

    public FieldDef()
    {
    }

    public static Type GetActualType(FieldType type, bool isArray)
    {
        return type switch
        {
            FieldType.Int => isArray ? typeof(List<int>) : typeof(int),
            FieldType.Float => isArray ? typeof(List<float>) : typeof(float),
            FieldType.String => isArray ? typeof(List<string>) : typeof(string),
            FieldType.Bool => isArray ? typeof(List<bool>) : typeof(bool),
            FieldType.Color => isArray ? typeof(List<Color>) : typeof(Color),
            FieldType.Point => isArray ? typeof(List<Point>) : typeof(Point),
            FieldType.Vector2 => isArray ? typeof(List<Vector2>) : typeof(Vector2),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static Color GetFieldColor(FieldType type)
    {
        return type switch
        {
            FieldType.Int => ImGuiExt.Colors[0],
            FieldType.Float => ImGuiExt.Colors[1],
            FieldType.String => ImGuiExt.Colors[2],
            FieldType.Bool => ImGuiExt.Colors[3],
            FieldType.Color => ImGuiExt.Colors[4],
            FieldType.Point => ImGuiExt.Colors[5],
            FieldType.Vector2 => ImGuiExt.Colors[6],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static object GetDefaultValue(FieldType type, bool isArray)
    {
        return type switch
        {
            FieldType.Int => isArray ? new List<int>() : default(int),
            FieldType.Float => isArray ? new List<float>() : default(float),
            FieldType.String => isArray ? new List<string>() : "",
            FieldType.Bool => isArray ? new List<bool>() : false,
            FieldType.Color => isArray ? new List<Color>() : Color.White,
            FieldType.Point => isArray ? new List<Point>() : Point.Zero,
            FieldType.Vector2 => isArray ? new List<Vector2>() : Vector2.Zero,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}

public class FieldInstance
{
    public int FieldDefId;
    public object? Value;
}

public class NewWorld
{
    public Guid Iid = Guid.NewGuid();
    public List<Level> Levels = new();
    public string Identifier = "World";

    public NewWorld()
    {
    }
}

public class Level
{
    public Guid Iid = Guid.NewGuid();
    public int Uid = IdGen.NewId;
    public Point WorldPos;
    public string Identifier = "Level";
    public uint Width;
    public uint Height;
    [JsonIgnore] public UPoint Size => new(Width, Height);
    public Color BackgroundColor = Color.White;

    public List<FieldInstance> FieldInstances = new();
    public List<LayerInstance> LayerInstances = new();

    public Level()
    {
    }
}

public class LayerInstance
{
    public int LayerDefId;
    public int[] IntGrid = Array.Empty<int>();
    public List<EntityInstance> EntityInstances = new();
}

public class EntityInstance
{
    public Guid Iid = Guid.NewGuid();
    public int EntityDefId;
    public List<FieldInstance> FieldInstances = new();
    public Point Position;
    public uint Width;
    public uint Height;
    [JsonIgnore] public UPoint Size => new(Width, Height);
}

public class EntityDefinition
{
    public List<FieldDef> FieldDefinitions = new();
    public Color Color;
    public uint Width;
    public uint Height;
    [JsonIgnore] public UPoint Size => new(Width, Height);
    public string Identifier = "";
    public float FillOpacity;
    public bool KeepAspectRatio;
    public bool ResizableX;
    public bool ResizableY;
    public bool ShowName;
    public uint TilesetId;
    public uint TileId;
    public double PivotX;
    public double PivotY;
    public List<string> Tags = new();

    public EntityDefinition()
    {
    }
}
