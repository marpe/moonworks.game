using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public class InspectorCallableInspector : Inspector
{
    private ParameterData[] _paramData = Array.Empty<ParameterData>();
    private string _buttonLabel = "Invoke";
    private string _headerLabel = "Invoke Method";

    private struct ParameterData
    {
        public string Name;
        public Func<object> Getter;
        public Action<object> Setter;
        public object Value;
        public Type ParameterType;
    }

    public override void Initialize()
    {
        var methodInfo = _memberInfo as MethodInfo ?? throw new InvalidOperationException();
        _buttonLabel = _callableAttr?.Label ?? methodInfo.Name;
        _headerLabel = $"Method: {_buttonLabel}";
        var methodParams = methodInfo.GetParameters();
        if (methodParams.Length > 0)
        {
            _paramData = new ParameterData[methodParams.Length];

            for (var i = 0; i < methodParams.Length; i++)
            {
                _paramData[i] = CreateParamData(methodParams[i], i);
            }
        }

        base.Initialize();
    }

    private ParameterData CreateParamData(ParameterInfo parameter, int index)
    {
        string name = parameter.Name ?? parameter.ParameterType.Name;
        object? value = parameter.HasDefaultValue ? parameter.DefaultValue : Activator.CreateInstance(parameter.ParameterType);
        return new ParameterData()
        {
            Getter = () => _paramData[index].Value,
            Setter = newValue => _paramData[index].Value = newValue,
            Name = name,
            Value = value ?? throw new InvalidOperationException(),
            ParameterType = parameter.ParameterType,
        };
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
        }

        if (_paramData.Length == 0)
        {
            DrawInvokeButton();
            return;
        }
        
        if (ImGuiExt.BeginCollapsingHeader(_headerLabel, ImGuiExt.Colors[1], ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont.Tiny))
        {
            for (var i = 0; i < _paramData.Length; i++)
            {
                var param = _paramData[i];

                SimpleTypeInspector.DrawSimpleInspector(
                    param.ParameterType,
                    param.Name,
                    param.Getter,
                    param.Setter,
                    false,
                    null
                );
            }

            DrawInvokeButton();

            ImGuiExt.EndCollapsingHeader();
        }
    }

    private void DrawInvokeButton()
    {
        if (ImGuiExt.ColoredButton(_buttonLabel, ImGuiExt.Colors[0], new Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            var parameters = _paramData.Select(x => x.Value).ToArray();
            var methodInfo = _memberInfo as MethodInfo ?? throw new InvalidOperationException();
            methodInfo.Invoke(_target, parameters);
        }
    }
}
