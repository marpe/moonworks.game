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
    private readonly List<IInspector> _inspectors = new();

    private bool _isInitialized;
    public Color HeaderColor = Color.DarkBlue;
    public bool ShowHeader = false;

    public GroupInspector()
    {
    }

    public GroupInspector(List<IInspector> inspectors)
    {
        _inspectors = inspectors;
        _isInitialized = true;
    }

    public int ChildCount => _inspectors.Count;

    public override void Initialize()
    {
        if (!_isInitialized)
        {
            ShowHeader = true;
            var value = GetValue();

            if (value != null)
            {
                var targetType = value.GetType();
                
                while (targetType != null && targetType != typeof(object))
                {
                    var inspector = InspectorExt.GetInspectorForTargetAndType(value, targetType);
                    if (inspector != null)
                    {
                        _inspectors.Add(inspector);
                    }

                    targetType = targetType.BaseType;
                }

                _inspectors.Reverse();
            }

            _isInitialized = true;
        }
    }

    public override void Draw()
    {
        Draw(0);
    }

    private void Draw(int depth)
    {
        var value = GetValue();
        if (value == null && ChildCount == 0)
        {
            ImGuiExt.DrawLabelWithCenteredText(Name, "NULL");
            return;
        }

        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
            if (ImGuiExt.Fold("GroupInspector Debug"))
            {
                var color = ImGui.GetColorU32(ImGuiCol.TextDisabled);
                ImGui.TextUnformatted($"NumSubInspectors: {_inspectors.Count}");
                ImGui.Checkbox("Show Header", ImGuiExt.RefPtr(ref ShowHeader));
                ImGuiExt.ColorEdit("Header Color", ref HeaderColor);
            }
        }

        if (ShowHeader)
        {
            if (ChildCount == 0)
            {
                ImGuiExt.DrawCollapsableLeaf(_name, HeaderColor);
            }
            else
            {
                if (ImGuiExt.BeginCollapsingHeader(_name, HeaderColor))
                {
                    DrawInspectors(ShowHeader, depth);
                    ImGuiExt.EndCollapsingHeader();
                }
            }
        }
        else
        {
            DrawInspectors(ShowHeader, depth);
        }
    }

    private void DrawInspectors(bool hasHeader, int depth)
    {
        for (var i = 0; i < _inspectors.Count; i++)
        {
            ImGui.PushID(i);

            if (_inspectors[i] is GroupInspector grpInspector)
            {
                grpInspector.HeaderColor = Temp.Colors[depth % Temp.Colors.Length];
                grpInspector.Draw(depth + 1);
                // if (hasHeader && grpInspector._inspectors.Count > 0)
                //     ImGuiExt.SeparatorText(_name + " End", HeaderColor, HeaderColor);
            }
            else
            {
                _inspectors[i].Draw();
            }

            ImGui.PopID();
        }
    }

    public void Filter(Func<MemberInfo?, bool> filter)
    {
        for (var i = _inspectors.Count - 1; i >= 0; i--)
        {
            if (filter(((Inspector)_inspectors[i]).MemberInfo))
            {
                _inspectors.RemoveAt(i);
            }
            else if (_inspectors[i] is GroupInspector grpInspector)
            {
                grpInspector.Filter(filter);
            }
        }
    }
}
