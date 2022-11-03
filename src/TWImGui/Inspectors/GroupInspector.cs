using ImGuiNET;

namespace MyGame.TWImGui.Inspectors;

public class GroupInspector : Inspector
{
	private List<IInspector> _inspectors = new();
	public Color HeaderColor = Color.DarkBlue;
	public bool ShowHeader;

	private bool _isInitialized;

	public int ChildCount => _inspectors.Count;

	public GroupInspector()
	{
	}

	public GroupInspector(List<IInspector> inspectors)
	{
		_inspectors = inspectors;
		_isInitialized = true;
	}

	public override void Initialize()
	{
		if (!_isInitialized)
		{
			ShowHeader = true;
			var value = GetValue();
			if (value != null)
			{
				var inspector = InspectorExt.GetInspectorForTargetAndType(value, value.GetType());
				if (inspector != null)
				{
					_inspectors.Add(inspector);
				}
			}

			_isInitialized = true;
		}
	}

	public override void Draw()
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
				ImGui.Checkbox("Show Header", ref ShowHeader);
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
					DrawInspectors();
					ImGuiExt.EndCollapsingHeader();
				}
			}
		}
		else
		{
			DrawInspectors();
		}
	}

	private void DrawInspectors()
	{
		for (var i = 0; i < _inspectors.Count; i++)
		{
			ImGui.PushID(i);
			_inspectors[i].Draw();
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
