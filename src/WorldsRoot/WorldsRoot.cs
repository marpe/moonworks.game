namespace MyGame.WorldsRoot;

public static class IdGen
{
    private static int _idCounter;
    public static int NewId => _idCounter++;
}

public class WorldsRoot
{
    public string Version = "0.0.1";
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
    public bool IsArray;
    public FieldType FieldType;

    public FieldDef()
    {
    }
}

public class FieldInstance
{
    public int FieldDefId;
    public dynamic Value = "";
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
    [JsonIgnore]
    public UPoint Size => new(Width, Height);
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
    [JsonIgnore]
    public UPoint Size => new(Width, Height);
}

public class EntityDefinition
{
    public List<FieldDef> FieldDefinitions = new();
    public Color Color;
    public uint Width;
    public uint Height;
    [JsonIgnore]
    public UPoint Size => new(Width, Height);
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
