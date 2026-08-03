using ApplicationServiceConsole.Application;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class ProjectChangeMonitor : IProjectChangeMonitor
{
    private static readonly HashSet<string> RelevantExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml", ".java", ".yml", ".yaml", ".json"
    };

    private readonly object _sync = new();
    private FileSystemWatcher? _watcher;
    private Timer? _timer;

    public event EventHandler? RelevantFileChanged;

    public void Start(string projectRoot)
    {
        StopMonitoring();
        _watcher = new(projectRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    public void Dispose()
    {
        StopMonitoring();
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void StopMonitoring() => DisposeWatcher();

    private void OnChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (IsRelevant(eventArgs.FullPath))
        {
            Debounce();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs eventArgs)
    {
        if (IsRelevant(eventArgs.FullPath) || IsRelevant(eventArgs.OldFullPath))
        {
            Debounce();
        }
    }

    private void Debounce()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = new(_ => RelevantFileChanged?.Invoke(this, EventArgs.Empty), null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }
    }

    private static bool IsRelevant(string path) =>
        string.Equals(Path.GetFileName(path), "pom.xml", StringComparison.OrdinalIgnoreCase)
        || RelevantExtensions.Contains(Path.GetExtension(path));

    private void DisposeWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
