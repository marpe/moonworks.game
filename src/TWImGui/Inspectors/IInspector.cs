namespace MyGame.TWImGui.Inspectors;

public interface IInspector
{
    string? InspectorOrder { get; set; }
    void Draw();
}
