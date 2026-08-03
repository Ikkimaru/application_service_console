using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IPortResolver
{
    Task<PortResolution> ResolveAsync(
        string projectRoot,
        ResolvedService service,
        string springProfile,
        CancellationToken cancellationToken = default);

    int FindAvailablePort();
}
