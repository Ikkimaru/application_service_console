using System.Text.Json;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class VsCodeLaunchMetadataProviderTests
{
    [Fact]
    public async Task EnrichesOnlyServicesThatWereActuallyDiscovered()
    {
        using TemporaryDirectory repository = new();
        string vscode = Path.Combine(repository.Path, ".vscode");
        Directory.CreateDirectory(vscode);
        await File.WriteAllTextAsync(Path.Combine(vscode, "launch.json"), """
            {
              "configurations": [
                { "name": "Spring Boot-RealApplication<real>", "projectName": "real", "mainClass": "com.example.RealApplication", "vmArgs": "-Xmx512m" },
                { "name": "Spring Boot-StaleApplication<stale>", "projectName": "stale", "mainClass": "com.example.StaleApplication" }
              ],
              "compounds": [
                { "name": "All Services", "configurations": ["Spring Boot-RealApplication<real>", "Spring Boot-StaleApplication<stale>"] }
              ]
            }
            """);
        DiscoveredService discovered = new()
        {
            Id = "com.example:real",
            GroupId = "com.example",
            ArtifactId = "real",
            ModulePath = "real",
            ApplicationEntryPoints = ["com.example.RealApplication"]
        };
        ProviderDefinition configuration = new()
        {
            Type = "vscodeJavaLaunch",
            Options = new(StringComparer.OrdinalIgnoreCase) { ["compoundName"] = "All Services" }
        };

        IReadOnlyDictionary<string, ServiceMetadata> result = await new VsCodeLaunchMetadataProvider()
            .EnrichAsync(repository.Path, configuration, [discovered]);

        ServiceMetadata metadata = Assert.Single(result).Value;
        Assert.True(metadata.ExplicitlyEnabled);
        Assert.Equal(0, metadata.Priority);
        Assert.Equal("-Xmx512m", metadata.JvmArguments);
        Assert.DoesNotContain(result.Keys, key => key.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }
}
