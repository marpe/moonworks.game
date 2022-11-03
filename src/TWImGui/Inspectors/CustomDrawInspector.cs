using MyGame.Utils;

namespace MyGame.TWImGui.Inspectors;

public class CustomDrawInspector : Inspector
{
	private MethodInfo? _drawMethodInfo;

	public override void Initialize()
	{
		var targetType = _target?.GetType() ?? _memberInfo?.DeclaringType;
		if (targetType == null)
		{
			throw new InvalidOperationException("Could not get target type");
		}

		if (_customDrawAttr == null)
		{
			throw new InvalidOperationException($"{nameof(CustomDrawInspectorAttribute)} was not found");
		}

		if (_customDrawAttr.MethodName != null)
		{
			_drawMethodInfo = ReflectionUtils.GetMethodInfo(targetType, _customDrawAttr.MethodName);
		}
		else
		{
			_drawMethodInfo = _memberInfo as MethodInfo;
		}
	}

	public override void Draw()
	{
		if (ImGuiExt.DebugInspectors)
		{
			DrawDebug();
		}

		if (_drawMethodInfo != null)
		{
			_drawMethodInfo.Invoke(_target, null);
		}
	}
}
