using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IServiceExecutor
{
    ExecutionCapability Capability { get; }
    event Action<ServiceExecutionState>? StateChanged;
    event Action<ServiceOutputLine>? OutputReceived;

    ServiceExecutionState GetState(string serviceId);
    Task<ExecutionPreflightResult> PreflightAsync(
        ResolvedService service,
        string projectRoot,
        CancellationToken cancellationToken = default);
    Task StartAsync(
        ResolvedService service,
        string projectRoot,
        int? portOverride,
        CancellationToken cancellationToken = default);
    Task StopAsync(string serviceId, CancellationToken cancellationToken = default);
}
