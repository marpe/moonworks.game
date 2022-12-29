using MyGame.Entities;

namespace MyGame.WorldsRoot;

public static class EntityDefinitions
{
    public static List<EntityDef> All = new();

    public static Dictionary<string, EntityDef> ByName = new();
    public static Dictionary<int, EntityDef> ById = new();

    public static Dictionary<string, Type> TypeMap = new();

    static EntityDefinitions()
    {
        var entityDefs = new Dictionary<Type, EntityDef>
        {
            {
                typeof(Player), new()
                {
                    Uid = 3,
                    Width = 8,
                    Height = 12,
                    Color = ColorExt.FromHex("61E6FF"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 0,
                    PivotX = 0.5,
                    PivotY = 1,
                    FillOpacity = 0.6f,
                }
            },
            {
                typeof(Slug), new()
                {
                    Uid = 7,
                    Width = 12,
                    Height = 10,
                    Color = ColorExt.FromHex("FF4B4B"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 12,
                    PivotX = 0.5,
                    PivotY = 1,
                    FillOpacity = 0.6f,
                }
            },
            {
                typeof(BlueBee), new()
                {
                    Uid = 8,
                    Width = 8,
                    Height = 8,
                    Color = ColorExt.FromHex("5C5CEC"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 10,
                    PivotX = 0.5,
                    PivotY = 0.5,
                    FillOpacity = 0.6f,
                }
            },
            {
                typeof(Light), new()
                {
                    Uid = 9,
                    Width = 16,
                    Height = 16,
                    Color = ColorExt.FromHex("FF9E31"),
                    ResizableX = true,
                    ResizableY = true,
                    KeepAspectRatio = true,
                    TileSetDefId = 5,
                    TileId = 117,
                    PivotX = 0.5,
                    PivotY = 0.5,
                    FillOpacity = 0.6f,
                    Tags = new() { "Light" }
                }
            },
            {
                typeof(Bullet), new()
                {
                    Uid = 20,
                    Width = 8,
                    Height = 8,
                    Color = ColorExt.FromHex("C52E20"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 4,
                    PivotX = 0.5,
                    PivotY = 0.5,
                    FillOpacity = 0.6f,
                }
            },
            {
                typeof(YellowBee), new()
                {
                    Uid = 21,
                    Width = 8,
                    Height = 8,
                    Color = ColorExt.FromHex("ffdd71"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 8,
                    PivotX = 0.5,
                    PivotY = 0.5,
                    FillOpacity = 0.6f,
                }
            },
            {
                typeof(MuzzleFlash), new()
                {
                    Uid = 22,
                    Width = 8,
                    Height = 8,
                    Color = ColorExt.FromHex("ffdd71"),
                    ResizableX = false,
                    ResizableY = false,
                    TileSetDefId = 6,
                    TileId = 3,
                    PivotX = 0.5,
                    PivotY = 0.5,
                    FillOpacity = 0.6f,
                }
            }
        };

        foreach (var (type, entityDef) in entityDefs)
        {
            All.Add(entityDef);
            entityDef.Identifier = type.Name;
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(x => x.IsDefined(typeof(InstanceFieldAttribute)));
            var i = 0;

            foreach (var member in members)
            {
                var (fieldType, isArray) = GetFieldType(member);
                var (min, max) = GetMinMax(member);

                var defaultValue = member switch
                {
                    FieldInfo field => field.GetValue(Activator.CreateInstance(type)),
                    PropertyInfo prop => prop.GetValue(Activator.CreateInstance(type)),
                    _ => throw new Exception(),
                };
                
                entityDef.FieldDefinitions.Add(new FieldDef()
                {
                    Uid = i++,
                    Identifier = member.Name,
                    FieldType = fieldType,
                    DefaultValue = defaultValue,
                    IsArray = isArray,
                    MinValue = min,
                    MaxValue = max,
                });
            }
            
            TypeMap.Add(entityDef.Identifier, type);
            ByName.Add(entityDef.Identifier, entityDef);
            ById.Add(entityDef.Uid, entityDef);
        }
    }
    
    
    private static (FieldType fieldType, bool isArray) GetFieldType(MemberInfo memberInfo)
    {
        var memberType = memberInfo switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo prop => prop.PropertyType,
            _ => throw new Exception(),
        };

        var isArray = memberType.IsArray;
        if (memberType.IsArray)
            memberType = memberType.GetElementType() ?? throw new Exception();
        FieldType fieldType;

        if (memberType == typeof(int))
            fieldType = FieldType.Int;
        else if (memberType == typeof(float))
            fieldType = FieldType.Float;
        else if (memberType == typeof(string))
            fieldType = FieldType.String;
        else if (memberType == typeof(bool))
            fieldType = FieldType.Bool;
        else if (memberType == typeof(Color))
            fieldType = FieldType.Color;
        else if (memberType == typeof(Point))
            fieldType = FieldType.Point;
        else if (memberType == typeof(Vector2))
            fieldType = FieldType.Vector2;
        else
            throw new Exception();

        return (fieldType, isArray);
    }

    private static (float min, float max) GetMinMax(MemberInfo memberInfo)
    {
        var rangeAttr = memberInfo.GetCustomAttribute<RangeAttribute>();
        return rangeAttr != null ? (rangeAttr.Settings.MinValue, rangeAttr.Settings.MaxValue) : (0, 0);
    }

    public static int Count => All.Count;
    public static EntityDef ByIndex(int i) => All[i];
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InstanceFieldAttribute : Attribute
{
}
