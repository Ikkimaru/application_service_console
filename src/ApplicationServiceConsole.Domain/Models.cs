namespace ApplicationServiceConsole.Domain;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public enum ServiceAvailability
{
    Discovered,
    Disabled,
    Unavailable,
    Ambiguous,
    Invalid
}

public sealed record ProviderDefinition
{
    public required string Type { get; init; }
    public Dictionary<string, string> Options { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ProfileVisualSettings
{
    public string Theme { get; init; } = "dark";
    public string AccentColor { get; init; } = "#5B8CFF";
    public string Icon { get; init; } = "server";
    public string Layout { get; init; } = "comfortable";
    public Dictionary<string, string> LogColors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ExecutionDefaults
{
    public string NewServicePolicy { get; init; } = "requireEnablement";
    public string SpringProfile { get; init; } = "local";
    public int MaximumParallelism { get; init; } = 1;
}

public sealed record CommandDefinition
{
    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public string? WorkingDirectory { get; init; }
    public string? EnvironmentFile { get; init; }
}

public sealed record ServiceOverrideDefinition
{
    public bool? Enabled { get; init; }
    public string? DisplayName { get; init; }
    public string? Group { get; init; }
    public int? Priority { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public CommandDefinition? Command { get; init; }
}

public sealed record ProjectProfile
{
    public int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ProjectRootSettingKey { get; init; }
    public IReadOnlyList<ProviderDefinition> DiscoveryProviders { get; init; } = Array.Empty<ProviderDefinition>();
    public IReadOnlyList<ProviderDefinition> MetadataProviders { get; init; } = Array.Empty<ProviderDefinition>();
    public ProfileVisualSettings Visual { get; init; } = new();
    public ExecutionDefaults Execution { get; init; } = new();
    public Dictionary<string, ServiceOverrideDefinition> ServiceOverrides { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ServiceDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? ServiceId = null);

public sealed record DiscoveredService
{
    public required string Id { get; init; }
    public required string GroupId { get; init; }
    public required string ArtifactId { get; init; }
    public required string ModulePath { get; init; }
    public IReadOnlyList<string> ApplicationEntryPoints { get; init; } = Array.Empty<string>();
    public CommandDefinition? SuggestedCommand { get; init; }
}

public sealed record ServiceMetadata
{
    public string? DisplayName { get; init; }
    public string? JvmArguments { get; init; }
    public string? EnvironmentFile { get; init; }
    public int? Priority { get; init; }
    public bool ExplicitlyEnabled { get; init; }
}

public sealed record ServiceDiscoveryResult(
    IReadOnlyList<DiscoveredService> Services,
    IReadOnlyList<ServiceDiagnostic> Diagnostics);

public sealed record ResolvedService
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Group { get; init; }
    public required string ModulePath { get; init; }
    public string? MainClass { get; init; }
    public bool IsEnabled { get; init; }
    public bool HasStartupMetadata { get; init; }
    public int Priority { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public CommandDefinition? Command { get; init; }
    public ServiceAvailability Availability { get; init; }
    public int? DiscoveredPort { get; init; }
}

public sealed record LocalProfileSettings
{
    public string? ProjectRoot { get; init; }
    public Dictionary<string, bool> ServiceEnablement { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record DiscoverySnapshot(
    ProjectProfile Profile,
    string? ProjectRoot,
    IReadOnlyList<ResolvedService> Services,
    IReadOnlyList<ServiceDiagnostic> Diagnostics,
    DateTimeOffset DiscoveredAt);

public sealed record PortResolution(int? Port, string Source, bool IsOccupied);

public sealed record ExecutionCapability(bool IsAvailable, string Reason);

public enum ServiceExecutionStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Exited,
    Failed
}

public sealed record ServiceExecutionState(
    string ServiceId,
    ServiceExecutionStatus Status,
    int? ProcessId = null,
    int? ExitCode = null,
    string? Message = null);

public sealed record ExecutionPreflightResult(bool CanStart, string Message);

public sealed record ServiceOutputLine(
    DateTimeOffset Timestamp,
    string ServiceId,
    string Stream,
    string Message);

public sealed record LogEvent(
    DateTimeOffset Timestamp,
    string ProfileId,
    string SessionId,
    string ServiceId,
    string Stream,
    string Level,
    string Message,
    long Sequence);
