using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface ILogSessionStore
{
    ExecutionCapability Capability { get; }
    Task AppendAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEvent>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
