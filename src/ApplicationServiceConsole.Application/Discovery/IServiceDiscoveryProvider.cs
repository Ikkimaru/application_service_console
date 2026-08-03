using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IServiceDiscoveryProvider
{
    string Type { get; }
    Task<ServiceDiscoveryResult> DiscoverAsync(
        string projectRoot,
        ProviderDefinition configuration,
        string springProfile,
        CancellationToken cancellationToken = default);
}
