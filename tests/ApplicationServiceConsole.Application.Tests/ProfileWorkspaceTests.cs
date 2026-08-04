using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application.Tests;

public sealed class ProfileWorkspaceTests
{
    [Fact]
    public async Task LaunchMetadataEnrichesGeneratedCommandWithoutReplacingDiscovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "asc-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ProjectProfile profile = new()
            {
                SchemaVersion = 1,
                Id = "test",
                Name = "Test",
                ProjectRootSettingKey = "test.root",
                DiscoveryProviders = [new ProviderDefinition { Type = "discovery" }],
                MetadataProviders = [new ProviderDefinition { Type = "metadata" }]
            };
            DiscoveredService discovered = new()
            {
                Id = "com.example:service",
                GroupId = "com.example",
                ArtifactId = "service",
                ModulePath = "service",
                ApplicationEntryPoints = ["com.example.ServiceApplication"],
                SuggestedCommand = new CommandDefinition
                {
                    FileName = "mvn.cmd",
                    WorkingDirectory = "${projectRoot}",
                    Arguments = ["spring-boot:run"]
                }
            };
            ProfileWorkspace workspace = new(
                [new FakeDiscoveryProvider(discovered)],
                [new FakeMetadataProvider(discovered.Id)],
                new FakeValidator(),
                new FakeSettingsStore(root),
                new FakePortResolver());

            DiscoverySnapshot snapshot = await workspace.LoadAsync(profile);

            ResolvedService service = Assert.Single(snapshot.Services);
            Assert.Equal(".env.local", service.Command?.EnvironmentFile);
            Assert.Contains("-Dspring-boot.run.jvmArguments=-Xmx512m", service.Command?.Arguments ?? []);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class FakeDiscoveryProvider(DiscoveredService service) : IServiceDiscoveryProvider
    {
        public string Type => "discovery";

        public Task<ServiceDiscoveryResult> DiscoverAsync(
            string projectRoot,
            ProviderDefinition configuration,
            string springProfile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceDiscoveryResult([service], []));
    }

    private sealed class FakeMetadataProvider(string serviceId) : IServiceMetadataProvider
    {
        public string Type => "metadata";

        public Task<IReadOnlyDictionary<string, ServiceMetadata>> EnrichAsync(
            string projectRoot,
            ProviderDefinition configuration,
            IReadOnlyList<DiscoveredService> services,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, ServiceMetadata>>(
                new Dictionary<string, ServiceMetadata>
                {
                    [serviceId] = new()
                    {
                        EnvironmentFile = ".env.local",
                        JvmArguments = "-Xmx512m",
                        ExplicitlyEnabled = true
                    }
                });
    }

    private sealed class FakeValidator : IProfileValidator
    {
        public IReadOnlyList<ServiceDiagnostic> Validate(ProjectProfile profile, IReadOnlyList<ResolvedService> services) => [];
    }

    private sealed class FakeSettingsStore(string projectRoot) : ILocalSettingsStore
    {
        public Task<LocalProfileSettings> GetAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LocalProfileSettings { ProjectRoot = projectRoot });

        public Task SaveAsync(string profileId, LocalProfileSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePortResolver : IPortResolver
    {
        public Task<PortResolution> ResolveAsync(
            string projectRoot,
            ResolvedService service,
            string springProfile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PortResolution(null, "unknown", false));

        public int FindAvailablePort() => 12345;
    }
}
