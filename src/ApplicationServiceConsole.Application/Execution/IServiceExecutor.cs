using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IServiceExecutor
{
    ExecutionCapability Capability { get; }
    Task StartAsync(ResolvedService service, int? portOverride, CancellationToken cancellationToken = default);
    Task StopAsync(string serviceId, CancellationToken cancellationToken = default);
}
