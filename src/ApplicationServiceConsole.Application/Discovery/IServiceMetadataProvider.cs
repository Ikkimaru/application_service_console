using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IServiceMetadataProvider
{
    string Type { get; }
    Task<IReadOnlyDictionary<string, ServiceMetadata>> EnrichAsync(
        string projectRoot,
        ProviderDefinition configuration,
        IReadOnlyList<DiscoveredService> services,
        CancellationToken cancellationToken = default);
}
