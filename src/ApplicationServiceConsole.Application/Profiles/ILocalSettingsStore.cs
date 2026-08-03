using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface ILocalSettingsStore
{
    Task<LocalProfileSettings> GetAsync(string profileId, CancellationToken cancellationToken = default);
    Task SaveAsync(string profileId, LocalProfileSettings settings, CancellationToken cancellationToken = default);
}
