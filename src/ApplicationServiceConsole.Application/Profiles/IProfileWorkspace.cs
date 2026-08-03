using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IProfileWorkspace
{
    Task<DiscoverySnapshot> LoadAsync(ProjectProfile profile, CancellationToken cancellationToken = default);
    Task SaveRootAsync(ProjectProfile profile, string projectRoot, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(ProjectProfile profile, string serviceId, bool enabled, CancellationToken cancellationToken = default);
}
