namespace MyGame.Coroutines;

public interface ICoroutine
{
	bool IsDone { get; set; }
	void Stop();
}
