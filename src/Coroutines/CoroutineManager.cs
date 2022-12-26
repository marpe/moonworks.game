namespace MyGame.Coroutines;

public class CoroutineManager : IDisposable
{
	private readonly List<Coroutine> _routines = new();
	private readonly List<Coroutine> _routinesToAddNextFrame = new();
	private readonly List<Coroutine> _routinesToUpdate = new();
	public bool IsDisposed { get; private set; }

	public Coroutine StartCoroutine(IEnumerator enumerator, float deltaSeconds = 0, [CallerArgumentExpression(nameof(enumerator))] string name = "")
	{
		if (IsDisposed)
			throw new ObjectDisposedException(nameof(CoroutineManager));

		var coroutine = new Coroutine(enumerator, name);
		coroutine.Tick(deltaSeconds);
		if (!coroutine.IsDone)
			_routinesToAddNextFrame.Add(coroutine);
		return coroutine;
	}

	public void StopAll()
	{
		foreach (var coroutine in _routines)
		{
			coroutine.Stop();
		}

		foreach (var coroutine in _routinesToAddNextFrame)
		{
			coroutine.Stop();
		}

		_routinesToAddNextFrame.Clear();
	}

	public void Dispose()
	{
		if (IsDisposed)
			return;

		_routines.Clear();
		_routinesToUpdate.Clear();
		_routinesToAddNextFrame.Clear();

		IsDisposed = true;
		GC.SuppressFinalize(this);
	}

	public void Update(float deltaSeconds)
	{
		if (IsDisposed)
			throw new ObjectDisposedException(nameof(CoroutineManager));

		_routinesToUpdate.Clear();
		_routinesToUpdate.AddRange(_routines);

		for (var i = 0; i < _routinesToUpdate.Count; i++)
		{
			var coroutine = _routinesToUpdate[i];
			coroutine.Tick(deltaSeconds);
			if (coroutine.IsDone)
				_routines.Remove(coroutine);
		}

		_routines.AddRange(_routinesToAddNextFrame);
		_routinesToAddNextFrame.Clear();
	}
}
