using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace MyGame.WorldsRoot;

public class RootJson
{
    public string Version = "0.0.1";
    public uint DefaultGridSize = 16;
    public List<World> Worlds = new();
    public List<EntityDefinition> EntityDefinitions = new();
    public List<FieldDef> LevelFieldDefinitions = new();
    public List<LayerDef> LayerDefinitions = new();
    public List<TileSetDef> TileSetDefinitions = new();
    
    [OnDeserialized]
    public void OnDeserialized(StreamingContext context)
    {
        for (var i = 0; i < EntityDefinitions.Count; i++)
        {
            var entityDef = EntityDefinitions[i];
            for (var j = 0; j < entityDef.FieldDefinitions.Count; j++)
            {
                var fieldDef = entityDef.FieldDefinitions[j];
            }
        }

        bool GetEntityDef(int entityDefUid, [NotNullWhen(true)] out EntityDefinition? entityDef)
        {
            for (var i = 0; i < EntityDefinitions.Count; i++)
            {
                if (EntityDefinitions[i].Uid == entityDefUid)
                {
                    entityDef = EntityDefinitions[i];
                    return true;
                }
            }

            entityDef = null;
            return false;
        }

        for (var i = 0; i < Worlds.Count; i++)
        {
            var world = Worlds[i];
            for (var j = 0; j < world.Levels.Count; j++)
            {
                var level = world.Levels[j];
                for (var k = 0; k < level.LayerInstances.Count; k++)
                {
                    var layerInstance = level.LayerInstances[k];
                    for (var l = 0; l < layerInstance.EntityInstances.Count; l++)
                    {
                        var entityInstance = layerInstance.EntityInstances[l];
                        if (!GetEntityDef(entityInstance.EntityDefId, out var entityDef))
                        {
                            continue;
                        }

                        EnsureFieldsAreValid(entityInstance, entityDef);
                    }
                }
            }
        }
    }

    private static bool GetFieldDef(int fieldDefId, EntityDefinition entityDef, [NotNullWhen(true)] out FieldDef? fieldDef)
    {
        for (var i = 0; i < entityDef.FieldDefinitions.Count; i++)
        {
            if (entityDef.FieldDefinitions[i].Uid == fieldDefId)
            {
                fieldDef = entityDef.FieldDefinitions[i];
                return true;
            }
        }

        fieldDef = null;
        return false;
    }

    public static void EnsureValueIsValid(ref object? value, FieldDef fieldDef, bool isArray)
    {
        if (value == null)
        {
            value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, isArray);
            return;
        }

        var actualType = FieldDef.GetActualType(fieldDef.FieldType, isArray);
        
        var instanceType = value.GetType();
        
        if (instanceType != actualType)
        {
            if (fieldDef.FieldType == FieldType.Color)
            {
                value = FieldDef.GetColor(value, Color.White);
                return;
            }
                
            value = Convert.ChangeType(value, actualType);
        }
    }
    
    public static void EnsureFieldsAreValid(EntityInstance entityInstance, EntityDefinition entityDef)
    {
        for (var i = 0; i < entityInstance.FieldInstances.Count; i++)
        {
            var fieldInstance = entityInstance.FieldInstances[i];
            if (!GetFieldDef(fieldInstance.FieldDefId, entityDef, out var fieldDef))
            {
                continue;
            }

            EnsureValueIsValid(ref fieldInstance.Value, fieldDef, fieldDef.IsArray);

            if (fieldDef.IsArray)
            {
                var list = (IList)fieldInstance.Value!;
                for (var j = 0; j < list.Count; j++)
                {
                    var value = list[j];
                    EnsureValueIsValid(ref value, fieldDef, false);
                    list[j] = value;
                }
            }
        }
    }
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

public enum EditorDisplayMode
{
    Hidden,
    ValueOnly,
    NameAndValue,
}

public class FieldDef
{
    public int Uid;
    public string Identifier = "Field";
    public FieldType FieldType;
    public object? DefaultValue;
    public bool IsArray;

    public float MinValue;
    public float MaxValue;
    public EditorDisplayMode EditorDisplayMode;

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

    public static Color GetColor(object? value, Color defaultColor)
    {
        return value switch
        {
            Color color => color,
            string colorStr when colorStr.StartsWith('#') => ColorExt.FromHex(colorStr.AsSpan().Slice(1)),
            _ => defaultColor,
        };
    }

    public static object GetDefaultValue(object? fieldDefaultValue, FieldType type, bool isArray)
    {
        return type switch
        {
            FieldType.Int => isArray ? new List<int>() : fieldDefaultValue ?? default(int),
            FieldType.Float => isArray ? new List<float>() : fieldDefaultValue ?? default(float),
            FieldType.String => isArray ? new List<string>() : fieldDefaultValue ?? "",
            FieldType.Bool => isArray ? new List<bool>() : fieldDefaultValue ?? false,
            FieldType.Color => isArray ? new List<Color>() : GetColor(fieldDefaultValue, Color.White),
            FieldType.Point => isArray ? new List<Point>() : fieldDefaultValue ?? Point.Zero,
            FieldType.Vector2 => isArray ? new List<Vector2>() : fieldDefaultValue ?? Vector2.Zero,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    [OnDeserialized]
    public void OnDeserialized(StreamingContext context)
    {
        RootJson.EnsureValueIsValid(ref DefaultValue, this, false);
    }
    
    public static FieldInstance CreateFieldInstance(FieldDef fieldDef)
    {
        return new FieldInstance
        {
            Value = GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray),
            FieldDefId = fieldDef.Uid
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
    public UPoint Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.X;
            Height = value.Y;
        }
    }

    [JsonIgnore] public Rectangle Bounds => new(WorldPos.X, WorldPos.Y, (int)Width, (int)Height);

    public Color BackgroundColor = Color.White;

    public List<FieldInstance> FieldInstances = new();
    public List<LayerInstance> LayerInstances = new();

    public Level()
    {
    }
}

public class AutoLayerTile
{
    public uint TileId;
    public UPoint Cell;
}

public class LayerInstance
{
    public int LayerDefId;
    public int[] IntGrid = Array.Empty<int>();
    public List<AutoLayerTile> AutoLayerTiles = new();
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

    [JsonIgnore]
    public UPoint Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.X;
            Height = value.Y;
        }
    }
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
    [JsonIgnore] public Vector2 Pivot => new Vector2(PivotX, PivotY);
    public List<string> Tags = new();

    public EntityDefinition()
    {
    }
    
    public static EntityInstance CreateEntityInstance(EntityDefinition entityDef)
    {
        var instance = new EntityInstance
        {
            Width = entityDef.Width,
            Height = entityDef.Height,
            EntityDefId = entityDef.Uid,
        };

        foreach (var fieldDef in entityDef.FieldDefinitions)
        {
            instance.FieldInstances.Add(FieldDef.CreateFieldInstance(fieldDef));
        }

        return instance;
    }
}

public struct AutoRuleTile
{
    public UPoint Cell;
    public int TileId;
    public int TileSetDefId;
    public Point LevelWorldPos;
    public uint LayerGridSize;
}
