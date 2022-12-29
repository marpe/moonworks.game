using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public unsafe class CollectionInspector : IInspectorWithTarget, IInspectorWithMemberInfo, IInspectorWithType
{
    public string? InspectorOrder { get; set; }
    private Dictionary<object, IInspector> _inspectors = new();
    private HashSet<object> _inactiveItems = new();

    private bool _isInitialized;
    private string _name = "";
    private Type? _type;
    private MemberInfo? _memberInfo;
    private object? _target;
    private string _keyHeader = "#";

    private ICollection? _collection;
    private bool _useFixedHeight = true;
    private int _fixedHeight = 100;

    public void SetType(Type type)
    {
        _type = type;
    }

    public void SetMemberInfo(MemberInfo memberInfo)
    {
        _memberInfo = memberInfo;
    }

    public void SetTarget(object target)
    {
        _target = target;
    }

    private void Initialize()
    {
        if (_memberInfo is FieldInfo field)
        {
            _collection = field.GetValue(_target) as ICollection;
            _name = field.Name;
        }
        else if (_memberInfo is PropertyInfo prop)
        {
            _collection = prop.GetValue(_target) as ICollection;
            _name = prop.Name;
        }
        else
        {
            throw new Exception();
        }

        // TODO (marpe): Check type and _target

        _isInitialized = true;
    }

    private static IEnumerator<(object key, object? item)> Enumerate(ICollection collection)
    {
        if (collection is IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                yield return (key, dictionary[key]);
            }

            yield break;
        }

        var i = 0;
        foreach (var value in collection)
        {
            yield return (i, value);
            i++;
        }
    }

    public void Draw()
    {
        if (!_isInitialized)
            Initialize();

        if (_collection == null)
        {
            ImGui.TextDisabled("NULL");
            return;
        }

        PushStyle();

        var foldLabel = $"{_name}[{_collection.Count}]";
        var isOpen = ImGuiExt.Fold(foldLabel);

        if (ImGui.BeginPopupContextItem())
        {
            ImGui.MenuItem("Fixed Height", default, ImGuiExt.RefPtr(ref _useFixedHeight));
            if (ImGui.MenuItem("Height: 400 ", default))
            {
                _fixedHeight = 400;
                _useFixedHeight = true;
            }

            if (ImGui.MenuItem("Height: 250 ", default))
            {
                _fixedHeight = 250;
                _useFixedHeight = true;
            }

            if (ImGui.MenuItem("Height: 100 ", default))
            {
                _fixedHeight = 100;
                _useFixedHeight = true;
            }
            ImGui.EndPopup();
        }

        if (isOpen)
        {
            DrawFoldout();
        }

        PopStyle();
        ImGui.Spacing();
    }

    private void DrawFoldout()
    {
        if (_collection!.Count == 0)
        {
            ImGui.TextDisabled("There are no items in this collection");
            return;
        }

        foreach (var item in _inspectors.Keys)
            _inactiveItems.Add(item);

        var tableSize = new Num.Vector2(0, _fixedHeight);
        if (!_useFixedHeight)
            tableSize.Y = 0;

        if (ImGui.BeginTable("Items", 2, ImGuiExt.DefaultTableFlags | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ContextMenuInBody, tableSize))
        {
            ImGui.TableSetupColumn(_keyHeader, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide, 20f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.NoHide);

            var enumerator = Enumerate(_collection);

            var clipper = new ImGuiListClipper();
            clipper.Begin(_collection.Count);
            var i = 0;
            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                {
                    while (i < row && enumerator.MoveNext())
                    {
                        i++;
                    }

                    if (enumerator.MoveNext())
                    {
                        ImGui.PushID(i++);
                        var (key, value) = enumerator.Current;
                        DrawItem(key, value);
                        ImGui.PopID();
                    }
                }
            }

            ImGui.EndTable();
        }

        foreach (var item in _inactiveItems)
            _inspectors.Remove(item);
        _inactiveItems.Clear();

        ImGuiExt.MediumVerticalSpace();
    }

    private static void PushStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Num.Vector2(0, 4));
    }

    private static void PopStyle()
    {
        ImGui.PopStyleVar(4);
    }

    private void SetValue(object key, object? item)
    {
        if (_collection is IDictionary dictionary)
        {
            dictionary[key] = item;
        }
        else if (_collection is IList list)
        {
            list[(int)key] = item;
        }
    }

    private void DrawItem(object key, object? item)
    {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.Text(key.ToString());

        ImGui.TableSetColumnIndex(1);

        if (item == null)
        {
            ImGui.TextDisabled("NULL");
        }
        else if (SimpleTypeInspector.SupportedTypes.Contains(item.GetType()))
        {
            ImGui.SetNextItemWidth(-ImGuiExt.FLT_MIN);
            SimpleTypeInspector.DrawSimpleInspector(
                item.GetType(),
                "##Value",
                () => item!,
                (newValue) => SetValue(key, newValue),
                false
            );
            ImGuiExt.ItemTooltip(key.ToString());
        }
        else if (!item.GetType().IsValueType)
        {
            if (!_inspectors.ContainsKey(item))
            {
                var inspector = InspectorExt.GetGroupInspectorForTarget(item);
                _inspectors.Add(item, inspector);
            }

            PopStyle();
            _inspectors[item].Draw();
            PushStyle();

            _inactiveItems.Remove(item);
        }
        else
        {
            ImGui.TextUnformatted(item.ToString());
        }
    }
}
