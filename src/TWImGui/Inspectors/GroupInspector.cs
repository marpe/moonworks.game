using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public static class Temp
{
    public static Color[] Colors =
    {
        new(32, 109, 255),
        new(11, 117, 196),
        new(0xA6, 0x1D, 0x1D),
        new(0x13, 0xAE, 0xB5),
        new(0xB5, 0x70, 0x27),
        new(0xAB, 0x10, 0x8F),
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

    public Color HeaderColor = new(32, 109, 255);
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

    public void SetShowHeaders(bool showHeaders, int fromDepth = 0, int depth = 0)
    {
        if (depth >= fromDepth)
        {
            ShowHeader = showHeaders;
        }

        foreach (var child in _inspectors)
        {
            if (child is GroupInspector childGroup)
            {
                childGroup.SetShowHeaders(showHeaders, fromDepth, depth + 1);
            }
        }
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
        Draw("", true, false);
    }

    private static int GetDepth(ReadOnlySpan<char> path)
    {
        var count = 0;
        for (var i = 0; i < path.Length; i++)
            if (path[i] == '/')
                count++;
        return count;
    }

    private void Draw(string path, bool drawWindow, bool rootIsWindow)
    {
        path += (path.Length > 0 ? '/' : "") + _name;
        var title = $"{_name} ({path})";

        DrawDebug(path, drawWindow, rootIsWindow);

        var flags = ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.DefaultOpen;
        ImGui.BeginGroup();

        if (ImGuiExt.BeginCollapsingHeader(title, HeaderColor, flags, ImGuiFont.Tiny, !ShowHeader))
        {
            if (_inspectors.Count == 0)
                ImGuiExt.SeparatorText("No inspectors found");

            for (var i = 0; i < _inspectors.Count; i++)
            {
                ImGui.PushID(i);

                if (_inspectors[i] is GroupInspector grpInspector)
                {
                    var depth = GetDepth(path);
                    grpInspector.HeaderColor = Temp.Colors[depth % Temp.Colors.Length];
                    grpInspector.Draw(path, drawWindow, rootIsWindow);
                }
                else
                {
                    _inspectors[i].Draw();
                }

                ImGui.PopID();
            }

            ImGuiExt.EndCollapsingHeader();
        }

        ImGui.EndGroup();
            
        ImGui.OpenPopupOnItemClick("Popup", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup | 
                                            ImGuiPopupFlags.NoOpenOverItems);

        if (drawWindow && _drawInSeparateWindow)
        {
            if (ImGuiExt.Begin($"{title}###GroupWindow{_id}", ref _drawInSeparateWindow))
            {
                Draw(path + "/Window", false, true);
            }

            ImGui.End();
        }
        
        if (ImGui.BeginPopup("Popup"))
        {
            ImGui.MenuItem($"Pop-out \"{_name}\"", default, ImGuiExt.RefPtr(ref _drawInSeparateWindow));
            ImGui.MenuItem($"Show Header for {_name}", default, ImGuiExt.RefPtr(ref ShowHeader));
            ImGui.EndPopup();
        }
    }

    private void DrawDebug(string path, bool drawWindow, bool rootIsWindow)
    {
        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
            if (ImGuiExt.Fold("GroupInspector Debug"))
            {
                ImGui.TextUnformatted($"NumSubInspectors: {_inspectors.Count}");
                ImGui.Checkbox("Show Header", ImGuiExt.RefPtr(ref ShowHeader));
                ImGuiExt.ColorEdit("Header Color", ref HeaderColor);
                ImGui.TextUnformatted($"Path: {path}");
                ImGui.TextUnformatted($"DrawWindow: {drawWindow.ToString()}");
                ImGui.TextUnformatted($"RootIsWindow: {rootIsWindow.ToString()}");
                ImGui.TextUnformatted($"DrawInSeparateWindow: {_drawInSeparateWindow.ToString()}");
            }
        }
    }
}
