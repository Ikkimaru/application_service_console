namespace ApplicationServiceConsole.Application;

public sealed record WindowLayoutSettings(double LeftSidebarWidth = 245, double RightSidebarWidth = 350);

public interface IWindowLayoutSettingsStore
{
    Task<WindowLayoutSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WindowLayoutSettings settings, CancellationToken cancellationToken = default);
}
