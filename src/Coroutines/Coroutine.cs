namespace MyGame.Coroutines;

public class Coroutine
{
    public string Name;
    private static readonly WaitHelper WaitHelper = new();
    private readonly Stack<IEnumerator> _enumerators = new();
    private Coroutine? _waitForCoroutine;
    private float _waitTimer;
    private bool _isCancelled;
    private float _elapsedTime = 0;
    public float ElapsedTime => _elapsedTime;
    public int NumUpdates { get; private set; }

    public bool IsDone { get; set; }
    public int NumEnumerators => _enumerators.Count;

    public Coroutine(IEnumerator enumerator, string name)
    {
        Name = name;
        _enumerators.Push(enumerator);
    }

    public void Stop()
    {
        IsDone = true;
    }

    public void Tick(float deltaSeconds, bool isUpdate = true)
    {
        while (true)
        {
            if (IsDone) return;

            if (isUpdate)
            {
                NumUpdates++;
                _elapsedTime += deltaSeconds;
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

            _isCancelled = false;
            var enumerator = _enumerators.Peek();
            if (!enumerator.MoveNext())
            {
                if (_isCancelled) // this routine got replaced
                    continue;
                _enumerators.Pop();
                IsDone = _enumerators.Count == 0;
                continue;
            }
            if (_isCancelled) // this routine got replaced
                continue;

            switch (enumerator.Current)
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
                    _enumerators.Push(childRoutine);
                    isUpdate = false;
                    continue;
                default:
                    throw new InvalidOperationException("Coroutine yielded unexpected value");
            }

            break;
        }
    }

    public void Replace(IEnumerator func, string name)
    {
        Name = name;
        _isCancelled = true;
        IsDone = false;
        NumUpdates = 0;
        _waitTimer = 0;
        _waitForCoroutine = null;
        _elapsedTime = 0;
        _enumerators.Clear();
        _enumerators.Push(func);
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
