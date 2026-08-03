namespace ApplicationServiceConsole.Application;

public interface IProjectChangeMonitor : IDisposable
{
    event EventHandler? RelevantFileChanged;
    void Start(string projectRoot);
    void StopMonitoring();
}
