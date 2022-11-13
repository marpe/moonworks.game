using Mochi.DearImGui;
using MyGame.Utils;

namespace MyGame.TWImGui.Inspectors;

public unsafe class InspectorCallableInspector : Inspector
{
    private string[] _invokeParamNames = Array.Empty<string>();
    private object?[] _invokeParams = Array.Empty<object>();

    public override void Initialize()
    {
        base.Initialize();

        var methodInfo = _memberInfo as MethodInfo ?? throw new InvalidOperationException();

        var methodParams = methodInfo.GetParameters();
        if (methodParams.Length > 0)
        {
            _invokeParams = new object[methodParams.Length];
            _invokeParamNames = new string[methodParams.Length];
            for (var i = 0; i < methodParams.Length; i++)
            {
                var parameter = methodParams[i];
                if (parameter.HasDefaultValue)
                {
                    _invokeParams[i] = parameter.DefaultValue;
                }
                else
                {
                    _invokeParams[i] = Activator.CreateInstance(parameter.ParameterType);
                }

                _invokeParamNames[i] = parameter.Name ?? parameter.ParameterType.Name;
            }
        }
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
        }

        var methodInfo = _memberInfo as MethodInfo ?? throw new InvalidOperationException();

        for (var i = 0; i < _invokeParams.Length; i++)
        {
            var invokeParam = _invokeParams[i];
            if (invokeParam is string strParam)
            {
                var inputBuffer = new ImGuiInputBuffer(strParam, 255);

                if (ImGui.InputText(_invokeParamNames[i], inputBuffer.Data, inputBuffer.Length))
                {
                    _invokeParams[i] = inputBuffer.ToString();
                }
            }
            else if (invokeParam is bool boolParam)
            {
                if (ImGui.Checkbox(_invokeParamNames[i], ImGuiExt.RefPtr(ref boolParam)))
                {
                    _invokeParams[i] = boolParam;
                }
            }
            else if (invokeParam is Vector2 vectorParam)
            {
                var asNumeric = vectorParam.ToNumerics();
                if (ImGui.DragFloat2(_name, ImGuiExt.RefPtr(ref asNumeric), 1.0f, 0, 0, "%g"))
                {
                    _invokeParams[i] = asNumeric.ToXNA();
                }
            }
            else if (invokeParam is float floatParam)
            {
                if (ImGui.InputFloat(_invokeParamNames[i], ImGuiExt.RefPtr(ref floatParam), default, default, default))
                {
                    _invokeParams[i] = floatParam;
                }
            }
        }

        if (ImGuiExt.ColoredButton(_callableAttr?.Label ?? methodInfo.Name))
        {
            methodInfo.Invoke(_target, _invokeParams);
        }
    }
}
