namespace MyGame.TWImGui.Inspectors;

public interface IInspector
{
	string? InspectorOrder { get; set; }
	string? Name { get; }
	void Draw();
}
