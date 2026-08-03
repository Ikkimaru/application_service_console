using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class MavenSpringBootDiscoveryProviderTests
{
    [Fact]
    public async Task DetectsAServiceAddedToTheMavenReactorWithoutInventoryChanges()
    {
        using TemporaryDirectory repository = new();
        WriteRootPom(repository.Path, ["services/first"]);
        WriteSpringModule(repository.Path, "services/first", "first-service", "FirstApplication");
        MavenSpringBootDiscoveryProvider provider = new();

        ServiceDiscoveryResult before = await provider.DiscoverAsync(repository.Path, Configuration(), "local");
        WriteRootPom(repository.Path, ["services/first", "services/new-service"]);
        WriteSpringModule(repository.Path, "services/new-service", "new-service", "NewServiceApplication");
        ServiceDiscoveryResult after = await provider.DiscoverAsync(repository.Path, Configuration(), "local");

        Assert.DoesNotContain(before.Services, service => service.Id == "com.example:new-service");
        Assert.Contains(after.Services, service => service.Id == "com.example:new-service");
    }

    [Fact]
    public async Task IgnoresLibrariesAndReportsAmbiguousApplications()
    {
        using TemporaryDirectory repository = new();
        WriteRootPom(repository.Path, ["library", "ambiguous"]);
        WriteModulePom(repository.Path, "library", "library");
        WriteSpringModule(repository.Path, "ambiguous", "ambiguous", "FirstApplication");
        WriteApplication(repository.Path, "ambiguous", "SecondApplication");
        MavenSpringBootDiscoveryProvider provider = new();

        ServiceDiscoveryResult result = await provider.DiscoverAsync(repository.Path, Configuration(), "local");

        Assert.DoesNotContain(result.Services, service => service.ArtifactId == "library");
        DiscoveredService ambiguous = Assert.Single(result.Services, service => service.ArtifactId == "ambiguous");
        Assert.Equal(2, ambiguous.ApplicationEntryPoints.Count);
    }

    [Fact]
    public async Task ReportsMalformedAndMissingDeclaredModulesWithoutCrashing()
    {
        using TemporaryDirectory repository = new();
        WriteRootPom(repository.Path, ["broken", "missing"]);
        string brokenDirectory = System.IO.Path.Combine(repository.Path, "broken");
        Directory.CreateDirectory(brokenDirectory);
        File.WriteAllText(System.IO.Path.Combine(brokenDirectory, "pom.xml"), "<project>");
        MavenSpringBootDiscoveryProvider provider = new();

        ServiceDiscoveryResult result = await provider.DiscoverAsync(repository.Path, Configuration(), "local");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MAVEN_POM_INVALID");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MAVEN_MODULE_MISSING");
    }

    private static ProviderDefinition Configuration() => new()
    {
        Type = "mavenSpringBoot",
        Options = new(StringComparer.OrdinalIgnoreCase) { ["searchRoot"] = "." }
    };

    private static void WriteRootPom(string root, IReadOnlyList<string> modules)
    {
        string moduleXml = string.Concat(modules.Select(module => $"<module>{module}</module>"));
        File.WriteAllText(System.IO.Path.Combine(root, "pom.xml"), $"""
            <project xmlns="http://maven.apache.org/POM/4.0.0">
              <modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>root</artifactId><version>1.0</version>
              <modules>{moduleXml}</modules>
            </project>
            """);
    }

    private static void WriteSpringModule(string root, string relativePath, string artifactId, string className)
    {
        WriteModulePom(root, relativePath, artifactId);
        WriteApplication(root, relativePath, className);
    }

    private static void WriteModulePom(string root, string relativePath, string artifactId)
    {
        string directory = System.IO.Path.Combine(root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Combine(directory, "pom.xml"), $"""
            <project xmlns="http://maven.apache.org/POM/4.0.0">
              <modelVersion>4.0.0</modelVersion>
              <parent><groupId>com.example</groupId><artifactId>root</artifactId><version>1.0</version></parent>
              <artifactId>{artifactId}</artifactId>
            </project>
            """);
    }

    private static void WriteApplication(string root, string relativePath, string className)
    {
        string javaDirectory = System.IO.Path.Combine(root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar), "src", "main", "java", "com", "example");
        Directory.CreateDirectory(javaDirectory);
        File.WriteAllText(System.IO.Path.Combine(javaDirectory, className + ".java"), $$"""
            package com.example;
            @SpringBootApplication
            public class {{className}} { }
            """);
    }
}
