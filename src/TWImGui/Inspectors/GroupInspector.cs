using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public static class Temp
{
    public static Color[] Colors =
    {
        Color.Fuchsia,
        Color.GreenYellow,
        Color.LightGray,
        Color.LightGreen,
        Color.Linen,
        Color.MintCream,
        Color.MistyRose,
        Color.NavajoWhite,
        Color.MediumBlue,
        Color.OliveDrab,
    };
}

public unsafe class GroupInspector : Inspector
{
    private static ulong IdCounter;
    private readonly List<IInspector> _inspectors = new();

    public Color HeaderColor = Color.DarkBlue;
    public bool ShowHeader;
    public bool _drawInSeparateWindow;
    private readonly string _id;

    public GroupInspector()
    {
        _id = IdCounter.ToString();
        IdCounter++;
    }

    public GroupInspector(List<IInspector> inspectors) : this()
    {
        _inspectors = inspectors;
    }

    private void AddInspector(IInspector inspector)
    {
        if (inspector is GroupInspector childGroup)
        {
            foreach (var child in childGroup._inspectors)
            {
                _inspectors.Add(child);
            }

            return;
        }

        _inspectors.Add(inspector);
    }

    private void AddInspectorsForEntireClassHierarchy(object value, Type type)
    {
        var t = type;
        while (t != null && t != typeof(object))
        {
            var inspector = InspectorExt.GetInspectorForTargetAndType(value, t);
            if (inspector != null)
            {
                AddInspector(inspector);
            }

            t = t.BaseType;
        }

        // reverse so that most primitive class members are at the top
        _inspectors.Reverse();
    }

    public override void Initialize()
    {
        base.Initialize();

        if (_inspectors.Count == 0 && _target != null && _targetType != null)
        {
            // having _valueType set means that this group inspector was created
            // for a member of another object (_memberInfo should also be set)
            // so we should continue the reflection
            if (_valueType != null)
            {
                var value = GetValue();
                if (value != null)
                {
                    AddInspectorsForEntireClassHierarchy(value, _valueType);
                }
            }
            else
            {
                var inspector = InspectorExt.GetInspectorForTargetAndType(_target, _targetType);
                if (inspector != null)
                {
                    AddInspector(inspector);
                }
            }
        }

        foreach (var inspector in _inspectors)
        {
            if (inspector is Inspector inspectorImpl)
            {
                inspectorImpl.Initialize();
            }
        }
    }

    public override void Draw()
    {
        Draw(0, "root");
    }

    private void Draw(int depth, string path)
    {
        var id = path + "_grp" + _id;
        DrawInternal(depth, id, false);

        if (!_drawInSeparateWindow)
            return;

        if (ImGuiExt.Begin(_name + "###" + id, ref _drawInSeparateWindow))
        {
            if (ImGui.BeginPopupContextWindow("ContextWindow"))
            {
                ImGui.MenuItem($"Pop-out \"{_name}\"", default, ImGuiExt.RefPtr(ref _drawInSeparateWindow));
                ImGui.EndPopup();
            }

            DrawInternal(depth, id, true);
        }

        ImGui.End();
    }

    private void DrawInternal(int depth, string id, bool isWindowed)
    {
        var value = GetValue();

        if (value == null)
        {
            ImGuiExt.DrawLabelWithCenteredText(_name, "NULL");
            return;
        }

        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
            if (ImGuiExt.Fold("GroupInspector Debug"))
            {
                ImGui.TextUnformatted($"NumSubInspectors: {_inspectors.Count}");
                ImGui.Checkbox("Show Header", ImGuiExt.RefPtr(ref ShowHeader));
                ImGuiExt.ColorEdit("Header Color", ref HeaderColor);
                ImGui.TextUnformatted($"IsWindowed: {isWindowed.ToString()}");
                ImGui.TextUnformatted($"DrawInSeparateWindow: {_drawInSeparateWindow.ToString()}");
            }
        }

        if (_inspectors.Count == 0)
        {
            // ImGuiExt.DrawCollapsableLeaf(_name, HeaderColor);
            if (ImGuiExt.BeginCollapsingHeader(_name, HeaderColor))
            {
                ImGuiExt.SeparatorText("No inspectors found");
                ImGuiExt.EndCollapsingHeader();
            }

            return;
        }

        // always skip first header in window mode
        if ((isWindowed && depth == 0) || !ShowHeader)
        {
            DrawInspectors(depth, id, isWindowed);
            return;
        }


        var result = ImGuiExt.BeginCollapsingHeader(_name + "##CollapsingHeader", HeaderColor, ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.SpanFullWidth);

        if (ImGui.BeginPopupContextItem("HeaderContextMenu"))
        {
            ImGui.MenuItem($"Pop-out \"{_name}\"", default, ImGuiExt.RefPtr(ref _drawInSeparateWindow));
            ImGui.EndPopup();
        }

        if (result)
        {
            DrawInspectors(depth, id, isWindowed);
            ImGuiExt.EndCollapsingHeader();
        }
    }

    private void DrawInspectors(int depth, string id, bool isWindowed)
    {
        for (var i = 0; i < _inspectors.Count; i++)
        {
            ImGui.PushID(i);

            if (_inspectors[i] is GroupInspector grpInspector)
            {
                grpInspector.HeaderColor = Temp.Colors[depth % Temp.Colors.Length];
                if (isWindowed)
                    grpInspector.DrawInternal(depth + 1, id, isWindowed);
                else
                    grpInspector.Draw(depth + 1, "root");
            }
            else
            {
                _inspectors[i].Draw();
            }

            ImGui.PopID();
        }
    }
}
