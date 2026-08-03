using System.Text.Json;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class JsonProfileCatalog : IProfileCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string[] _profileDirectories;

    public JsonProfileCatalog(IEnumerable<string> profileDirectories)
    {
        _profileDirectories = profileDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<ProjectProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, ProjectProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (string profileDirectory in _profileDirectories)
        {
            if (!Directory.Exists(profileDirectory))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(profileDirectory, "*.profile.json", SearchOption.AllDirectories))
            {
                await using FileStream stream = File.OpenRead(path);
                ProjectProfile? profile = await JsonSerializer.DeserializeAsync<ProjectProfile>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                if (profile is not null)
                {
                    profiles[profile.Id] = profile;
                }
            }
        }

        return profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
