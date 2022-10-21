namespace MyGame.TWImGui;

public class ImGuiCallbackWindow : ImGuiWindow
{
	private Action<ImGuiWindow> _callback;

	public ImGuiCallbackWindow(string title, Action<ImGuiWindow> callback) : base(title)
	{
		_callback = callback;
	}

	public override void Draw()
	{
		_callback.Invoke(this);
	}
}
