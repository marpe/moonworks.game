using System.Threading;

namespace MyGame.Editor;

public record struct FileEvent(string FullPath, float CallAtTime);

public class FileWatcher : IDisposable
{
    private Action<string>? _callback;
    private FileSystemWatcher? _watcher;

    private Dictionary<string, FileEvent> _eventQueue = new();
    private readonly string _fullPath;
    private readonly Timer _timer;

    public bool IsDisposed { get; private set; }

    public FileWatcher(string path, string filter, Action<string> callback)
    {
        _timer = new Timer(TimerCallback, null, 500, Timeout.Infinite);

        _callback = callback;

        _fullPath = Path.GetFullPath(path);
        _watcher = new FileSystemWatcher(_fullPath);

        _watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;

        _watcher.Changed += OnChanged;
        _watcher.Error += OnError;
        _watcher.Created += OnCreated;
        _watcher.Renamed += OnRenamed;
        _watcher.Disposed += OnDisposed;

        Logs.LogInfo($"Watching \"{path}\" ({_fullPath}) with filter {filter}");

        _watcher.Filter = filter;
        _watcher.IncludeSubdirectories = true;
        _watcher.EnableRaisingEvents = true;
    }

    private void TimerCallback(object? state)
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _eventQueue)
        {
            if (kvp.Value.CallAtTime <= Shared.Game.Time.TotalElapsedTime)
            {
                InvokeCallback(kvp.Value.FullPath);
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _eventQueue.Remove(key);
        }

        _timer.Change(500, Timeout.Infinite);
    }

    private void OnDisposed(object? sender, EventArgs e)
    {
        Logs.LogInfo($"Stopped watching {_fullPath}");
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        QueueFileEvent(e.FullPath);
    }

    private static bool IsFileLocked(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open);
        }
        catch (IOException)
        {
            return true;
        }

        return false;
    }

    private void InvokeCallback(string fullPath)
    {
        if (IsFileLocked(fullPath))
        {
            Logs.LogError($"File is locked ({fullPath}), ignoring...");
            return;
        }

        Logs.LogInfo($"Invoking callback for {fullPath}");
        _callback?.Invoke(fullPath);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        QueueFileEvent(e.FullPath);
    }

    private void OnError(object sender, ErrorEventArgs e) => PrintException(e.GetException());

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }

        QueueFileEvent(e.FullPath);
    }

    private void QueueFileEvent(string fullPath)
    {
        var deferredDuration = 1f;
        _eventQueue[fullPath] = new FileEvent(fullPath, Shared.Game.Time.TotalElapsedTime + deferredDuration);
    }

    private static void PrintException(Exception? ex)
    {
        if (ex != null)
        {
            Logs.LogError($"Message: {ex.Message}");
            Logs.LogError("Stacktrace:");
            Logs.LogError(ex.StackTrace ?? "");
            Logs.LogError("\n");
            PrintException(ex.InnerException);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _timer.Dispose();
            _watcher?.Dispose();
            _watcher = null;
            _callback = null;
        }

        IsDisposed = true;
    }
}
