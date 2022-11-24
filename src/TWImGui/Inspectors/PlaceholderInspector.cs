using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public class PlaceholderInspector : IInspector
{
    public string? InspectorOrder { get; set; }
    public string? Name { get; } = "Placeholder";
    public Color TextColor = Color.Red;
    public string Message;

    public PlaceholderInspector(string message)
    {
        Message = message;
    }
    
    public void Draw()
    {
        ImGui.TextColored(TextColor.ToNumerics(), Message);
    }
}
