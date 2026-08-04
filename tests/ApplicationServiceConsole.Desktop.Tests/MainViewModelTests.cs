using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task FirstLaunchOpensFolderPickerWhenAProfileNeedsItsRoot()
    {
        ProjectProfile profile = new()
        {
            SchemaVersion = 1,
            Id = "local-maven",
            Name = "Local Maven Project",
            ProjectRootSettingKey = "local-maven.root",
            DiscoveryProviders = [new ProviderDefinition { Type = "mavenSpringBoot" }]
        };
        FakeWorkspace workspace = new(profile);
        FakeFolderPicker folderPicker = new(Path.GetTempPath());
        using FakeChangeMonitor monitor = new();
        using MainViewModel viewModel = new(new FakeProfileCatalog(profile), workspace, folderPicker, monitor, new FakeExecutor());

        await viewModel.InitializeAsync();

        Assert.Equal(1, folderPicker.InvocationCount);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), workspace.ProjectRoot);
    }

    private sealed class FakeProfileCatalog(ProjectProfile profile) : IProfileCatalog
    {
        public Task<IReadOnlyList<ProjectProfile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectProfile>>([profile]);
    }

    private sealed class FakeFolderPicker(string path) : IFolderPicker
    {
        public int InvocationCount { get; private set; }

        public string? PickProjectRoot(string? initialDirectory = null)
        {
            InvocationCount++;
            return path;
        }
    }

    private sealed class FakeWorkspace(ProjectProfile profile) : IProfileWorkspace
    {
        public string? ProjectRoot { get; private set; }

        public Task<DiscoverySnapshot> LoadAsync(ProjectProfile requestedProfile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiscoverySnapshot(profile, ProjectRoot, [], [], DateTimeOffset.UtcNow));

        public Task SaveRootAsync(ProjectProfile requestedProfile, string projectRoot, CancellationToken cancellationToken = default)
        {
            ProjectRoot = Path.GetFullPath(projectRoot);
            return Task.CompletedTask;
        }

        public Task SetEnabledAsync(ProjectProfile requestedProfile, string serviceId, bool enabled, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeChangeMonitor : IProjectChangeMonitor
    {
        public event EventHandler? RelevantFileChanged
        {
            add { }
            remove { }
        }

        public void Start(string projectRoot)
        {
        }

        public void StopMonitoring()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeExecutor : IServiceExecutor
    {
        public ExecutionCapability Capability { get; } = new(true, "Available for testing.");

        public event Action<ServiceExecutionState>? StateChanged
        {
            add { }
            remove { }
        }

        public event Action<ServiceOutputLine>? OutputReceived
        {
            add { }
            remove { }
        }

        public ServiceExecutionState GetState(string serviceId) => new(serviceId, ServiceExecutionStatus.Stopped);

        public Task<ExecutionPreflightResult> PreflightAsync(
            ResolvedService service,
            string projectRoot,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExecutionPreflightResult(true, "Ready."));

        public Task StartAsync(
            ResolvedService service,
            string projectRoot,
            int? portOverride,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string serviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
