using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IProfileCatalog
{
    Task<IReadOnlyList<ProjectProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
}
