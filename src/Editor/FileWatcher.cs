using System.Collections.Concurrent;
using System.Threading;

namespace MyGame.Editor;

public record struct FileEvent(string FullPath, float CallAtTime, WatcherChangeTypes ChangeType);

public sealed class FileWatcher : IDisposable
{
    private Action<FileEvent>? _callback;
    private FileSystemWatcher? _watcher;

    private ConcurrentDictionary<string, FileEvent> _eventQueue = new();
    private readonly string _fullPath;
    private readonly Timer _timer;
    private List<string> _tempKeysToRemove = new();

    public bool IsDisposed { get; private set; }

    public FileWatcher(string path, string filter, Action<FileEvent> callback)
    {
        _timer = new Timer(TimerCallback, null, 500, Timeout.Infinite);

        _callback = callback;

        _fullPath = Path.GetFullPath(path);
        _watcher = new FileSystemWatcher(_fullPath);

        _watcher.NotifyFilter = NotifyFilters.FileName
                                | NotifyFilters.LastWrite
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
        _tempKeysToRemove.Clear();
        foreach (var kvp in _eventQueue)
        {
            if (kvp.Value.CallAtTime <= Shared.Game.Time.TotalElapsedTime)
            {
                InvokeCallback(kvp.Value);
                _tempKeysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in _tempKeysToRemove)
        {
            _eventQueue.TryRemove(key, out _);
        }

        _timer.Change(500, Timeout.Infinite);
    }

    private void OnDisposed(object? sender, EventArgs e)
    {
        Logs.LogInfo($"Stopped watching {_fullPath}");
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        QueueFileEvent(e.FullPath, e.ChangeType);
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
        catch (UnauthorizedAccessException)
        {
            return true;
        }

        return false;
    }

    private void InvokeCallback(in FileEvent fileChangedEvent)
    {
        if (IsFileLocked(fileChangedEvent.FullPath))
        {
            Logs.LogError($"File is locked ({fileChangedEvent.FullPath}), ignoring...");
            return;
        }

        Logs.LogInfo($"Invoking callback for {fileChangedEvent.FullPath} ({fileChangedEvent.ChangeType})");
        _callback?.Invoke(fileChangedEvent);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        QueueFileEvent(e.FullPath, e.ChangeType);
    }

    private void OnError(object sender, ErrorEventArgs e) => PrintException(e.GetException());

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        QueueFileEvent(e.FullPath, e.ChangeType);
    }

    private void QueueFileEvent(string fullPath, WatcherChangeTypes changeType)
    {
        var deferredDuration = 1f;
        var addValue = new FileEvent(fullPath, Shared.Game.Time.TotalElapsedTime + deferredDuration, changeType);

        _ = _eventQueue.AddOrUpdate(
            fullPath,
            addValue,
            (fileEvent, existingEvent) =>
            {
                existingEvent.CallAtTime += 1f;
                return existingEvent;
            });
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
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _eventQueue.Clear();
            _timer.Dispose();
            _watcher?.Dispose();
            _watcher = null;
            _callback = null;
        }

        IsDisposed = true;
    }
}
