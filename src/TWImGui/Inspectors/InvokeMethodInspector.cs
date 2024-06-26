using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public class InvokeMethodInspector : IInspectorWithTarget, IInspectorWithMemberInfo
{
    public string? InspectorOrder { get; set; }

    private ParameterData[] _paramData = Array.Empty<ParameterData>();
    private string _buttonLabel = "Invoke";
    private string _headerLabel = "Invoke Method";

    private object _target = null!;
    private MethodInfo _methodInfo = null!;

    private struct ParameterData
    {
        public string Name;
        public Func<object> Getter;
        public Action<object> Setter;
        public object Value;
        public Type ParameterType;
    }

    public void SetMemberInfo(MemberInfo memberInfo)
    {
        _methodInfo = (MethodInfo)memberInfo;
        var attr = _methodInfo.GetCustomAttribute<InspectorCallableAttribute>() ?? throw new InvalidOperationException("Attribute not found");
        _buttonLabel = attr.Label ?? _methodInfo.Name;
        var methodParams = _methodInfo.GetParameters();
        _headerLabel = $"{_buttonLabel}{PrettyPrintParameters(methodParams)}";

        if (methodParams.Length > 0)
        {
            _paramData = new ParameterData[methodParams.Length];

            for (var i = 0; i < methodParams.Length; i++)
            {
                var pInfo = methodParams[i];
                _paramData[i] = CreateParamData(pInfo, i);
            }
        }
    }

    private static string PrettyPrintParameters(ParameterInfo[] methodParams)
    {
        var sb = new StringBuilder();
        sb.Append('(');

        for (var i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];
            sb.Append(ConsoleUtils.GetDisplayName(param.ParameterType));
            sb.Append(' ');
            sb.Append(param.Name);
                
            if (param.HasDefaultValue)
            {
                sb.Append(" = ");
                sb.Append(param.DefaultValue?.ToString() ?? "NULL");
            }

            if (i < methodParams.Length - 1)
                sb.Append(", ");
        }
        
        sb.Append(')');
        return sb.ToString();
    }

    public void SetTarget(object target)
    {
        _target = target;
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

    public void Draw()
    {
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
        if (ImGuiExt.ColoredButton(_buttonLabel, new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            var parameters = _paramData.Select(x => x.Value).ToArray();
            _methodInfo.Invoke(_target, parameters);
        }
    }
}
