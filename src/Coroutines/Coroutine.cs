namespace MyGame.Coroutines;

public class Coroutine : ICoroutine
{
	private static readonly WaitHelper WaitHelper = new();
	private readonly IEnumerator _enumerator;
	private Coroutine? _childRoutine;
	private Coroutine? _waitForCoroutine;
	private float _waitTimer;
	public int NumUpdates { get; private set; }

	public bool IsDone { get; set; }

	public Coroutine(IEnumerator enumerator)
	{
		_enumerator = enumerator;
	}

	public void Stop()
	{
		IsDone = true;
	}

	public void Tick(float deltaSeconds, bool isUpdate = true)
	{
		if (IsDone)
			return;

		if(isUpdate)
			NumUpdates++;

		if (_childRoutine != null)
		{
			_childRoutine.Tick(deltaSeconds);
			if (_childRoutine.IsDone)
				_childRoutine = null;
			else
				return;
		}

		if (_waitForCoroutine != null)
		{
			if (_waitForCoroutine.IsDone)
				_waitForCoroutine = null;
			else
				return;
		}

		if (_waitTimer > 0)
		{
			_waitTimer -= deltaSeconds;
			return;
		}

		if (!_enumerator.MoveNext())
		{
			IsDone = true;
			return;
		}

		switch (_enumerator.Current)
		{
			case null:
				// noop
				break;
			case WaitHelper waitForSeconds:
				_waitTimer = waitForSeconds.WaitTime;
				break;
			case Coroutine runner:
				_waitForCoroutine = runner;
				break;
			case IEnumerator childRoutine:
				var child = new Coroutine(childRoutine);
				child.Tick(deltaSeconds);
				if (!child.IsDone)
					_childRoutine = child;
				else
					Tick(deltaSeconds, false); // continue execution if the child routine isn't running for more than 1 frame
				break;
			default:
				throw new InvalidOperationException("Coroutine yielded unexpected value");
		}
	}

	public static WaitHelper WaitForSeconds(float seconds)
	{
		WaitHelper.WaitTime = seconds;
		return WaitHelper;
	}
}

public class WaitHelper
{
	public float WaitTime;
}
