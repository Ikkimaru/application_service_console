using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public sealed class ProfileWorkspace(
    IEnumerable<IServiceDiscoveryProvider> discoveryProviders,
    IEnumerable<IServiceMetadataProvider> metadataProviders,
    IProfileValidator validator,
    ILocalSettingsStore settingsStore,
    IPortResolver portResolver) : IProfileWorkspace
{
    private readonly Dictionary<string, IServiceDiscoveryProvider> _discoveryProviders =
        discoveryProviders.ToDictionary(provider => provider.Type, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IServiceMetadataProvider> _metadataProviders =
        metadataProviders.ToDictionary(provider => provider.Type, StringComparer.OrdinalIgnoreCase);

    public async Task<DiscoverySnapshot> LoadAsync(ProjectProfile profile, CancellationToken cancellationToken = default)
    {
        LocalProfileSettings localSettings = await settingsStore.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        List<ServiceDiagnostic> diagnostics = [];

        if (string.IsNullOrWhiteSpace(localSettings.ProjectRoot) || !Directory.Exists(localSettings.ProjectRoot))
        {
            diagnostics.Add(new(DiagnosticSeverity.Warning, "PROJECT_ROOT_REQUIRED", "Choose the local project folder to discover services."));
            diagnostics.AddRange(validator.Validate(profile, []));
            return new(profile, localSettings.ProjectRoot, [], diagnostics, DateTimeOffset.UtcNow);
        }

        string projectRoot = Path.GetFullPath(localSettings.ProjectRoot);
        List<DiscoveredService> discovered = [];
        foreach (ProviderDefinition configuration in profile.DiscoveryProviders)
        {
            if (!_discoveryProviders.TryGetValue(configuration.Type, out IServiceDiscoveryProvider? provider))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "DISCOVERY_PROVIDER_UNKNOWN", $"Discovery provider '{configuration.Type}' is unavailable."));
                continue;
            }

            ServiceDiscoveryResult result = await provider.DiscoverAsync(projectRoot, configuration, profile.Execution.SpringProfile, cancellationToken).ConfigureAwait(false);
            discovered.AddRange(result.Services);
            diagnostics.AddRange(result.Diagnostics);
        }

        IReadOnlyList<DiscoveredService> uniqueServices = RemoveDuplicates(discovered, diagnostics);
        Dictionary<string, ServiceMetadata> metadata = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProviderDefinition configuration in profile.MetadataProviders)
        {
            if (!_metadataProviders.TryGetValue(configuration.Type, out IServiceMetadataProvider? provider))
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "METADATA_PROVIDER_UNKNOWN", $"Metadata provider '{configuration.Type}' is unavailable."));
                continue;
            }

            IReadOnlyDictionary<string, ServiceMetadata> values =
                await provider.EnrichAsync(projectRoot, configuration, uniqueServices, cancellationToken).ConfigureAwait(false);
            foreach ((string id, ServiceMetadata value) in values)
            {
                metadata[id] = value;
            }
        }

        List<ResolvedService> resolved = [];
        foreach (DiscoveredService service in uniqueServices)
        {
            profile.ServiceOverrides.TryGetValue(service.Id, out ServiceOverrideDefinition? profileOverride);
            metadata.TryGetValue(service.Id, out ServiceMetadata? enrichment);
            localSettings.ServiceEnablement.TryGetValue(service.Id, out bool localEnabled);
            bool hasLocalEnablement = localSettings.ServiceEnablement.ContainsKey(service.Id);
            bool enabled = profileOverride?.Enabled ?? (hasLocalEnablement ? localEnabled : enrichment?.ExplicitlyEnabled ?? false);
            bool ambiguous = service.ApplicationEntryPoints.Count != 1;
            CommandDefinition? command = ResolveCommand(profileOverride?.Command, service.SuggestedCommand, enrichment);

            ResolvedService item = new()
            {
                Id = service.Id,
                DisplayName = profileOverride?.DisplayName ?? enrichment?.DisplayName ?? Humanize(service.ArtifactId),
                Group = profileOverride?.Group ?? GuessGroup(service.ModulePath),
                ModulePath = service.ModulePath,
                MainClass = service.ApplicationEntryPoints.SingleOrDefault(),
                IsEnabled = enabled,
                HasStartupMetadata = profileOverride?.Priority is not null || enrichment?.Priority is not null,
                Priority = profileOverride?.Priority ?? enrichment?.Priority ?? int.MaxValue,
                DependsOn = profileOverride?.DependsOn ?? [],
                Command = command,
                Availability = ambiguous ? ServiceAvailability.Ambiguous : enabled ? ServiceAvailability.Discovered : ServiceAvailability.Disabled
            };

            if (ambiguous)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "ENTRY_POINT_AMBIGUOUS", "A service must have exactly one Spring Boot application entry point.", service.Id));
            }

            PortResolution port = await portResolver.ResolveAsync(projectRoot, item, profile.Execution.SpringProfile, cancellationToken).ConfigureAwait(false);
            item = item with { DiscoveredPort = port.Port };
            if (port.IsOccupied && port.Port is not null)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "PORT_OCCUPIED", $"Port {port.Port} is already in use. A temporary free port can be selected when starting.", service.Id));
            }

            resolved.Add(item);
        }

        diagnostics.AddRange(validator.Validate(profile, resolved));
        return new(
            profile,
            projectRoot,
            resolved.OrderBy(service => service.Priority).ThenBy(service => service.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics,
            DateTimeOffset.UtcNow);
    }

    public async Task SaveRootAsync(ProjectProfile profile, string projectRoot, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(projectRoot);
        LocalProfileSettings current = await settingsStore.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        await settingsStore.SaveAsync(profile.Id, current with { ProjectRoot = fullPath }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetEnabledAsync(ProjectProfile profile, string serviceId, bool enabled, CancellationToken cancellationToken = default)
    {
        LocalProfileSettings current = await settingsStore.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<string, bool> values = new(current.ServiceEnablement, StringComparer.OrdinalIgnoreCase)
        {
            [serviceId] = enabled
        };
        await settingsStore.SaveAsync(profile.Id, current with { ServiceEnablement = values }, cancellationToken).ConfigureAwait(false);
    }

    private static List<DiscoveredService> RemoveDuplicates(
        IEnumerable<DiscoveredService> services,
        List<ServiceDiagnostic> diagnostics)
    {
        List<DiscoveredService> unique = [];
        foreach (IGrouping<string, DiscoveredService> group in services.GroupBy(service => service.Id, StringComparer.OrdinalIgnoreCase))
        {
            unique.Add(group.First());
            if (group.Skip(1).Any())
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "SERVICE_ID_DUPLICATE", $"More than one discovered module uses id '{group.Key}'.", group.Key));
            }
        }

        return unique;
    }

    private static string GuessGroup(string path)
    {
        string normalized = path.Replace('\\', '/');
        int separator = normalized.IndexOf('/');
        return separator > 0 ? Humanize(normalized[..separator]) : "Services";
    }

    private static string Humanize(string value) =>
        string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static CommandDefinition? ResolveCommand(
        CommandDefinition? executionOverride,
        CommandDefinition? suggestedCommand,
        ServiceMetadata? metadata)
    {
        if (executionOverride is not null || suggestedCommand is null)
        {
            return executionOverride ?? suggestedCommand;
        }

        IReadOnlyList<string> arguments = string.IsNullOrWhiteSpace(metadata?.JvmArguments)
            ? suggestedCommand.Arguments
            : suggestedCommand.Arguments.Concat([$"-Dspring-boot.run.jvmArguments={metadata.JvmArguments}"]).ToArray();
        return suggestedCommand with
        {
            Arguments = arguments,
            EnvironmentFile = metadata?.EnvironmentFile ?? suggestedCommand.EnvironmentFile
        };
    }
}
