using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application.Tests;

public sealed class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new();

    [Fact]
    public void RejectsDependencyCycles()
    {
        ProjectProfile profile = CreateProfile();
        ResolvedService first = CreateService("example:first", ["example:second"]);
        ResolvedService second = CreateService("example:second", ["example:first"]);

        IReadOnlyList<ServiceDiagnostic> diagnostics = _validator.Validate(profile, [first, second]);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "DEPENDENCY_CYCLE");
    }

    [Fact]
    public void RejectsUnknownDependenciesAndUnsupportedSchema()
    {
        ProjectProfile profile = CreateProfile() with { SchemaVersion = 2 };
        ResolvedService service = CreateService("example:first", ["example:missing"]);

        IReadOnlyList<ServiceDiagnostic> diagnostics = _validator.Validate(profile, [service]);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "PROFILE_SCHEMA_UNSUPPORTED");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "DEPENDENCY_UNKNOWN");
    }

    private static ProjectProfile CreateProfile() => new()
    {
        SchemaVersion = 1,
        Id = "example",
        Name = "Example",
        ProjectRootSettingKey = "example.root",
        DiscoveryProviders = [new ProviderDefinition { Type = "mavenSpringBoot" }]
    };

    private static ResolvedService CreateService(string id, IReadOnlyList<string> dependencies) => new()
    {
        Id = id,
        DisplayName = id,
        Group = "Tests",
        ModulePath = id,
        IsEnabled = true,
        HasStartupMetadata = true,
        Priority = 0,
        DependsOn = dependencies,
        Availability = ServiceAvailability.Discovered
    };
}
