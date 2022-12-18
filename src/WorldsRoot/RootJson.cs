namespace MyGame.WorldsRoot;

public class RootJson
{
    public string Version = "0.0.1";
    public int DefaultGridSize = 16;
    public List<World> Worlds = new();
    public List<EntityDefinition> EntityDefinitions = new();
    public List<FieldDef> LevelFieldDefinitions = new();
    public List<LayerDef> LayerDefinitions = new();
    public List<TileSetDef> TileSetDefinitions = new();
}

public class TileSetDef
{
    public int Uid;
    public string Identifier = "TileSet";
    public string Path = "";
    public uint TileGridSize = 16;
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
    public int Uid;
    public LayerType LayerType = LayerType.IntGrid;
    public string Identifier = "Layer";
    public List<string> RequiredTags = new();
    public List<string> ExcludedTags = new();
    public uint GridSize = 16;
    public List<IntGridValue> IntGridValues = new();
    public uint TileSetDefId;

    public List<AutoRuleGroup> AutoRuleGroups = new();
    
    public LayerDef()
    {
    }
}

public class AutoRuleGroup
{
    public int Uid;
    public string Name = "New Group";
    public List<AutoRule> Rules = new();
    public bool IsActive;
}

public class AutoRule
{
    public int Uid;
    public int Size = 3;
    public bool IsActive;
    public bool BreakOnMatch = true;
    public List<int> Pattern = new();
    public List<int> TileIds = new();
    public float Chance = 1.0f;
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
    public int Uid;
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

public class World
{
    public Guid Iid = Guid.NewGuid();
    public List<Level> Levels = new();
    public string Identifier = "World";

    public World()
    {
    }
}

public class Level
{
    public Guid Iid = Guid.NewGuid();
    public int Uid;
    public Point WorldPos;
    public string Identifier = "Level";
    public uint Width;
    public uint Height;
    [JsonIgnore]
    public UPoint Size => new(Width, Height);

    [JsonIgnore] public Rectangle Bounds => new(WorldPos.X, WorldPos.Y, (int)Width, (int)Height);
    
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

    public bool IsVisible = true;
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
    public int Uid;
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
    public uint TileSetDefId;
    public uint TileId;
    public double PivotX;
    public double PivotY;
    public List<string> Tags = new();

    public EntityDefinition()
    {
    }
}
