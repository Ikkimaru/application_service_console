namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class JsonProfileCatalogTests
{
    [Fact]
    public async Task LoadsProfilesFromLocalDirectoriesWithLaterSourcesTakingPrecedence()
    {
        using TemporaryDirectory root = new();
        string repositoryProfiles = Path.Combine(root.Path, "profiles.local");
        string applicationDataProfiles = Path.Combine(root.Path, "application-data");
        Directory.CreateDirectory(repositoryProfiles);
        Directory.CreateDirectory(applicationDataProfiles);
        await File.WriteAllTextAsync(
            Path.Combine(repositoryProfiles, "sample.profile.json"),
            CreateProfile("Repository profile"));
        await File.WriteAllTextAsync(
            Path.Combine(applicationDataProfiles, "sample.profile.json"),
            CreateProfile("Application data profile"));

        JsonProfileCatalog catalog = new([repositoryProfiles, applicationDataProfiles]);
        ApplicationServiceConsole.Domain.ProjectProfile profile = Assert.Single(await catalog.GetProfilesAsync());

        Assert.Equal("Application data profile", profile.Name);
    }

    private static string CreateProfile(string name) => $$"""
        {
          "schemaVersion": 1,
          "id": "sample",
          "name": "{{name}}",
          "projectRootSettingKey": "sample.root",
          "discoveryProviders": [{ "type": "mavenSpringBoot" }]
        }
        """;
}
