using System.Text.Json;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class VsCodeLaunchMetadataProvider : IServiceMetadataProvider
{
    public string Type => "vscodeJavaLaunch";

    public async Task<IReadOnlyDictionary<string, ServiceMetadata>> EnrichAsync(
        string projectRoot,
        ProviderDefinition configuration,
        IReadOnlyList<DiscoveredService> services,
        CancellationToken cancellationToken = default)
    {
        string relativePath = configuration.Options.GetValueOrDefault("path", ".vscode/launch.json");
        string path = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        if (!File.Exists(path))
        {
            return new Dictionary<string, ServiceMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        await using FileStream stream = File.OpenRead(path);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true },
            cancellationToken).ConfigureAwait(false);

        Dictionary<string, JsonElement> configurations = new(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("configurations", out JsonElement configurationArray))
        {
            foreach (JsonElement item in configurationArray.EnumerateArray())
            {
                if (item.TryGetProperty("name", out JsonElement name))
                {
                    configurations[name.GetString() ?? string.Empty] = item;
                }
            }
        }

        Dictionary<string, int> compoundOrder = ReadCompoundOrder(document.RootElement, configuration.Options.GetValueOrDefault("compoundName", string.Empty));
        Dictionary<string, ServiceMetadata> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (DiscoveredService service in services)
        {
            KeyValuePair<string, JsonElement>? match = configurations.FirstOrDefault(pair => Matches(service, pair.Value));
            if (match is null || string.IsNullOrWhiteSpace(match.Value.Key))
            {
                continue;
            }

            JsonElement value = match.Value.Value;
            bool inCompound = compoundOrder.TryGetValue(match.Value.Key, out int priority);
            result[service.Id] = new()
            {
                DisplayName = GetDisplayName(match.Value.Key),
                JvmArguments = GetString(value, "vmArgs"),
                EnvironmentFile = GetString(value, "envFile"),
                ExplicitlyEnabled = inCompound,
                Priority = inCompound ? priority : null
            };
        }

        return result;
    }

    private static bool Matches(DiscoveredService service, JsonElement configuration)
    {
        string? projectName = GetString(configuration, "projectName");
        string? mainClass = GetString(configuration, "mainClass");
        return string.Equals(projectName, service.ArtifactId, StringComparison.OrdinalIgnoreCase)
            || service.ApplicationEntryPoints.Contains(mainClass ?? string.Empty, StringComparer.Ordinal);
    }

    private static Dictionary<string, int> ReadCompoundOrder(JsonElement root, string compoundName)
    {
        Dictionary<string, int> result = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(compoundName) || !root.TryGetProperty("compounds", out JsonElement compounds))
        {
            return result;
        }

        foreach (JsonElement compound in compounds.EnumerateArray())
        {
            if (!string.Equals(GetString(compound, "name"), compoundName, StringComparison.OrdinalIgnoreCase)
                || !compound.TryGetProperty("configurations", out JsonElement names))
            {
                continue;
            }

            int priority = 0;
            foreach (JsonElement name in names.EnumerateArray())
            {
                string? value = name.GetString();
                if (value is not null)
                {
                    result[value] = priority++;
                }
            }
        }

        return result;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) ? property.GetString() : null;

    private static string GetDisplayName(string launchName)
    {
        int dash = launchName.IndexOf('-', StringComparison.Ordinal);
        int angle = launchName.IndexOf('<');
        return dash >= 0 && angle > dash ? launchName[(dash + 1)..angle] : launchName;
    }
}
