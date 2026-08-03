using System.Text.RegularExpressions;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public sealed partial class ProfileValidator : IProfileValidator
{
    public IReadOnlyList<ServiceDiagnostic> Validate(ProjectProfile profile, IReadOnlyList<ResolvedService> services)
    {
        List<ServiceDiagnostic> diagnostics = [];

        if (profile.SchemaVersion != 1)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "PROFILE_SCHEMA_UNSUPPORTED", $"Profile schema {profile.SchemaVersion} is not supported."));
        }

        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "PROFILE_IDENTITY_INVALID", "Profile id and name are required."));
        }

        if (profile.DiscoveryProviders.Count == 0)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "DISCOVERY_PROVIDER_MISSING", "At least one discovery provider is required."));
        }

        if (!HexColorRegex().IsMatch(profile.Visual.AccentColor))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "ACCENT_COLOR_INVALID", "Accent color must use #RRGGBB format."));
        }

        Dictionary<string, ResolvedService> byId = services.ToDictionary(service => service.Id, StringComparer.OrdinalIgnoreCase);
        foreach (ResolvedService service in services)
        {
            foreach (string dependency in service.DependsOn)
            {
                if (!byId.ContainsKey(dependency))
                {
                    diagnostics.Add(new(DiagnosticSeverity.Error, "DEPENDENCY_UNKNOWN", $"Dependency '{dependency}' was not discovered.", service.Id));
                }
            }
        }

        DetectCycles(byId, diagnostics);
        return diagnostics;
    }

    private static void DetectCycles(
        IReadOnlyDictionary<string, ResolvedService> services,
        List<ServiceDiagnostic> diagnostics)
    {
        Dictionary<string, int> state = new(StringComparer.OrdinalIgnoreCase);

        bool Visit(string id)
        {
            if (state.TryGetValue(id, out int current))
            {
                return current == 1;
            }

            state[id] = 1;
            if (services.TryGetValue(id, out ResolvedService? service))
            {
                foreach (string dependency in service.DependsOn.Where(services.ContainsKey))
                {
                    if (Visit(dependency))
                    {
                        return true;
                    }
                }
            }

            state[id] = 2;
            return false;
        }

        foreach (string id in services.Keys)
        {
            if (Visit(id))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "DEPENDENCY_CYCLE", "The service dependency graph contains a cycle."));
                return;
            }
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
