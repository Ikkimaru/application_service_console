using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApplicationServiceConsole.Desktop;

public sealed record ProjectProfileViewModel(ProjectProfile Definition)
{
    public string Name => Definition.Name;
    public string Description => Definition.Description ?? Definition.Id;
}

public sealed record DiagnosticViewModel(ServiceDiagnostic Diagnostic)
{
    public string Heading => $"{Diagnostic.Severity.ToString().ToUpperInvariant()} • {Diagnostic.Code}";
    public string Message => Diagnostic.Message;
    public string ServiceId => Diagnostic.ServiceId ?? string.Empty;
}

public sealed record ExecutionLogLineViewModel(ServiceOutputLine Line)
{
    public string DisplayText => $"{Line.Timestamp:HH:mm:ss.fff}  {Line.ServiceId}  {Line.Stream}  {Line.Message}";
}

public sealed partial class ServiceItemViewModel : ObservableObject
{
    private readonly Func<ServiceItemViewModel, Task> _toggle;
    private readonly Func<ServiceItemViewModel, Task> _start;
    private readonly Func<ServiceItemViewModel, Task> _stop;
    private readonly Func<ServiceItemViewModel, Task> _restart;
    private readonly ExecutionCapability _capability;

    public ServiceItemViewModel(
        ResolvedService service,
        Func<ServiceItemViewModel, Task> toggle,
        Func<ServiceItemViewModel, Task> start,
        Func<ServiceItemViewModel, Task> stop,
        Func<ServiceItemViewModel, Task> restart,
        ExecutionCapability capability,
        ServiceExecutionState executionState)
    {
        Service = service;
        _toggle = toggle;
        _start = start;
        _stop = stop;
        _restart = restart;
        _capability = capability;
        ExecutionState = executionState;
    }

    public ResolvedService Service { get; }
    public ServiceExecutionState ExecutionState { get; private set; }
    public string Id => Service.Id;
    public string DisplayName => Service.DisplayName;
    public string ModulePath => Service.ModulePath;
    public string Status => ExecutionState.Status == ServiceExecutionStatus.Stopped
        ? Service.Availability.ToString()
        : ExecutionState.Status.ToString();
    public string PortText => Service.DiscoveredPort?.ToString(CultureInfo.InvariantCulture) ?? "Auto / unknown";
    public string EnableActionText => Service.IsEnabled ? "Disable" : "Enable";
    public string StartToolTip => CanStart()
        ? "Start this service."
        : _capability.IsAvailable ? "Enable a valid service with a start command first." : _capability.Reason;

    [RelayCommand(CanExecute = nameof(CanToggleEnabled))]
    private Task ToggleEnabledAsync() => _toggle(this);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => _start(this);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => _stop(this);

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private Task RestartAsync() => _restart(this);

    public void UpdateExecutionState(ServiceExecutionState state)
    {
        ExecutionState = state;
        OnPropertyChanged(nameof(ExecutionState));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StartToolTip));
        ToggleEnabledCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
    }

    private bool CanToggleEnabled() => !IsActive();

    private bool CanStart() =>
        _capability.IsAvailable
        && Service.IsEnabled
        && Service.Availability == ServiceAvailability.Discovered
        && Service.Command is not null
        && ExecutionState.Status is ServiceExecutionStatus.Stopped or ServiceExecutionStatus.Exited or ServiceExecutionStatus.Failed;

    private bool CanStop() => ExecutionState.Status is ServiceExecutionStatus.Starting or ServiceExecutionStatus.Running;

    private bool CanRestart() => ExecutionState.Status == ServiceExecutionStatus.Running;

    private bool IsActive() => ExecutionState.Status is
        ServiceExecutionStatus.Starting or ServiceExecutionStatus.Running or ServiceExecutionStatus.Stopping;
}

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaximumLiveOutputLines = 500;
    private readonly IProfileCatalog _profileCatalog;
    private readonly IProfileWorkspace _workspace;
    private readonly IFolderPicker _folderPicker;
    private readonly IProjectChangeMonitor _changeMonitor;
    private readonly IServiceExecutor _serviceExecutor;
    private readonly List<ServiceItemViewModel> _allServices = [];
    private DiscoverySnapshot? _snapshot;
    private bool _initialized;
    private bool _isInitializing;

    public MainViewModel(
        IProfileCatalog profileCatalog,
        IProfileWorkspace workspace,
        IFolderPicker folderPicker,
        IProjectChangeMonitor changeMonitor,
        IServiceExecutor serviceExecutor)
    {
        _profileCatalog = profileCatalog;
        _workspace = workspace;
        _folderPicker = folderPicker;
        _changeMonitor = changeMonitor;
        _serviceExecutor = serviceExecutor;
        _changeMonitor.RelevantFileChanged += OnRelevantFileChanged;
        _serviceExecutor.StateChanged += OnExecutionStateChanged;
        _serviceExecutor.OutputReceived += OnOutputReceived;
    }

    public ObservableCollection<ProjectProfileViewModel> Profiles { get; } = [];
    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];
    public ObservableCollection<DiagnosticViewModel> Diagnostics { get; } = [];
    public ObservableCollection<ExecutionLogLineViewModel> LiveOutput { get; } = [];

    [ObservableProperty]
    private ProjectProfileViewModel? selectedProfile;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Loading project profiles…";

    public string ServiceSummary =>
        $"{_allServices.Count} discovered • {_allServices.Count(service => service.Service.IsEnabled)} enabled • "
        + $"{_allServices.Count(service => service.ExecutionState.Status == ServiceExecutionStatus.Running)} running";
    public string DiagnosticSummary => Diagnostics.Count == 0 ? "No problems detected" : $"{Diagnostics.Count} item(s) require attention";
    public string LiveOutputStatus => LiveOutput.Count == 0 ? "No process output yet." : $"Showing the latest {LiveOutput.Count} lines.";

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _isInitializing = true;
        IReadOnlyList<ProjectProfile> profiles = await _profileCatalog.GetProfilesAsync();
        foreach (ProjectProfile profile in profiles)
        {
            Profiles.Add(new(profile));
        }

        SelectedProfile = Profiles.FirstOrDefault();
        _isInitializing = false;
        if (SelectedProfile is null)
        {
            StatusMessage = "No portable profiles were found.";
            return;
        }

        await RefreshAsync();
        if (string.IsNullOrWhiteSpace(_snapshot?.ProjectRoot))
        {
            await ChooseRootAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedProfile is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _snapshot = await _workspace.LoadAsync(SelectedProfile.Definition);
            ReplaceSnapshot(_snapshot);
            string? projectRoot = _snapshot.ProjectRoot;
            if (!string.IsNullOrWhiteSpace(projectRoot) && Directory.Exists(projectRoot))
            {
                _changeMonitor.Start(projectRoot);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Diagnostics.Clear();
            Diagnostics.Add(new(new(DiagnosticSeverity.Error, "DISCOVERY_FAILED", exception.Message)));
            StatusMessage = "Discovery failed. Review the diagnostics.";
            OnPropertyChanged(nameof(DiagnosticSummary));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseRootAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        string? selectedPath = _folderPicker.PickProjectRoot(_snapshot?.ProjectRoot);
        if (selectedPath is null)
        {
            return;
        }

        await _workspace.SaveRootAsync(SelectedProfile.Definition, selectedPath);
        await RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedProfileChanged(ProjectProfileViewModel? value)
    {
        if (_initialized && !_isInitializing && value is not null)
        {
            _changeMonitor.StopMonitoring();
            _ = RefreshCommand.ExecuteAsync(null);
        }
    }

    private void ReplaceSnapshot(DiscoverySnapshot snapshot)
    {
        _allServices.Clear();
        _allServices.AddRange(snapshot.Services.Select(service => new ServiceItemViewModel(
            service,
            ToggleServiceAsync,
            StartServiceAsync,
            StopServiceAsync,
            RestartServiceAsync,
            _serviceExecutor.Capability,
            _serviceExecutor.GetState(service.Id))));
        Diagnostics.Clear();
        foreach (ServiceDiagnostic diagnostic in snapshot.Diagnostics.OrderByDescending(item => item.Severity))
        {
            Diagnostics.Add(new(diagnostic));
        }

        ApplyFilter();
        StatusMessage = snapshot.ProjectRoot is null
            ? "Choose a project folder to begin discovery."
            : $"Scanned {snapshot.ProjectRoot} at {snapshot.DiscoveredAt:HH:mm:ss}";
        OnPropertyChanged(nameof(ServiceSummary));
        OnPropertyChanged(nameof(DiagnosticSummary));
    }

    private void ApplyFilter()
    {
        IEnumerable<ServiceItemViewModel> filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allServices
            : _allServices.Where(service =>
                service.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || service.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || service.ModulePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        Services.Clear();
        foreach (ServiceItemViewModel service in filtered)
        {
            Services.Add(service);
        }
    }

    private async Task ToggleServiceAsync(ServiceItemViewModel service)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await _workspace.SetEnabledAsync(SelectedProfile.Definition, service.Id, !service.Service.IsEnabled);
        await RefreshAsync();
    }

    private async Task StartServiceAsync(ServiceItemViewModel service)
    {
        DiscoverySnapshot? snapshot = _snapshot;
        string? projectRoot = snapshot?.ProjectRoot;
        if (snapshot is null || string.IsNullOrWhiteSpace(projectRoot))
        {
            AddExecutionDiagnostic(service.Id, "EXECUTION_ROOT_REQUIRED", "Choose a project folder before starting a service.");
            return;
        }

        if (snapshot.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "PORT_OCCUPIED"
            && string.Equals(diagnostic.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase)))
        {
            AddExecutionDiagnostic(
                service.Id,
                "EXECUTION_PORT_OCCUPIED",
                "The discovered port is occupied. Temporary port selection will be added in the next milestone.");
            return;
        }

        try
        {
            ExecutionPreflightResult preflight = await _serviceExecutor.PreflightAsync(service.Service, projectRoot);
            if (!preflight.CanStart)
            {
                AddExecutionDiagnostic(service.Id, "EXECUTION_PREFLIGHT_FAILED", preflight.Message);
                return;
            }

            await _serviceExecutor.StartAsync(service.Service, projectRoot, null);
            StatusMessage = $"Starting {service.DisplayName}.";
        }
        catch (Exception exception) when (IsExpectedExecutionException(exception))
        {
            AddExecutionDiagnostic(service.Id, "EXECUTION_START_FAILED", exception.Message);
        }
    }

    private async Task StopServiceAsync(ServiceItemViewModel service)
    {
        try
        {
            await _serviceExecutor.StopAsync(service.Id);
            StatusMessage = $"Stopped {service.DisplayName}.";
        }
        catch (Exception exception) when (IsExpectedExecutionException(exception))
        {
            AddExecutionDiagnostic(service.Id, "EXECUTION_STOP_FAILED", exception.Message);
        }
    }

    private async Task RestartServiceAsync(ServiceItemViewModel service)
    {
        await StopServiceAsync(service);
        await StartServiceAsync(service);
    }

    private void AddExecutionDiagnostic(string serviceId, string code, string message)
    {
        Diagnostics.Add(new(new(DiagnosticSeverity.Error, code, message, serviceId)));
        StatusMessage = "Service execution needs attention. Review the diagnostics.";
        OnPropertyChanged(nameof(DiagnosticSummary));
    }

    private void OnExecutionStateChanged(ServiceExecutionState state) => DispatchToUi(() =>
    {
        ServiceItemViewModel? service = _allServices.FirstOrDefault(item =>
            string.Equals(item.Id, state.ServiceId, StringComparison.OrdinalIgnoreCase));
        service?.UpdateExecutionState(state);
        if (state.Status == ServiceExecutionStatus.Failed && state.Message is not null)
        {
            AddExecutionDiagnostic(state.ServiceId, "PROCESS_EXITED", state.Message);
        }

        OnPropertyChanged(nameof(ServiceSummary));
    });

    private void OnOutputReceived(ServiceOutputLine line) => DispatchToUi(() =>
    {
        LiveOutput.Add(new(line));
        while (LiveOutput.Count > MaximumLiveOutputLines)
        {
            LiveOutput.RemoveAt(0);
        }

        OnPropertyChanged(nameof(LiveOutputStatus));
    });

    private static bool IsExpectedExecutionException(Exception exception) => exception is
        InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception;

    private static void DispatchToUi(Action action)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = dispatcher.InvokeAsync(action);
        }
    }

    private void OnRelevantFileChanged(object? sender, EventArgs eventArgs)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            _ = dispatcher.InvokeAsync(() => RefreshCommand.Execute(null));
        }
    }

    public void Dispose()
    {
        _changeMonitor.RelevantFileChanged -= OnRelevantFileChanged;
        _serviceExecutor.StateChanged -= OnExecutionStateChanged;
        _serviceExecutor.OutputReceived -= OnOutputReceived;
        _changeMonitor.Dispose();
    }
}
