using System.Collections.ObjectModel;
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

public sealed partial class ServiceItemViewModel : ObservableObject
{
    private readonly Func<ServiceItemViewModel, Task> _toggle;

    public ServiceItemViewModel(ResolvedService service, Func<ServiceItemViewModel, Task> toggle)
    {
        Service = service;
        _toggle = toggle;
    }

    public ResolvedService Service { get; private set; }
    public string Id => Service.Id;
    public string DisplayName => Service.DisplayName;
    public string ModulePath => Service.ModulePath;
    public string Status => Service.Availability.ToString();
    public string PortText => Service.DiscoveredPort?.ToString(CultureInfo.InvariantCulture) ?? "Auto / unknown";
    public string EnableActionText => Service.IsEnabled ? "Disable" : "Enable";

    [RelayCommand]
    private Task ToggleEnabledAsync() => _toggle(this);
}

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IProfileCatalog _profileCatalog;
    private readonly IProfileWorkspace _workspace;
    private readonly IFolderPicker _folderPicker;
    private readonly IProjectChangeMonitor _changeMonitor;
    private readonly List<ServiceItemViewModel> _allServices = [];
    private DiscoverySnapshot? _snapshot;
    private bool _initialized;
    private bool _isInitializing;

    public MainViewModel(
        IProfileCatalog profileCatalog,
        IProfileWorkspace workspace,
        IFolderPicker folderPicker,
        IProjectChangeMonitor changeMonitor)
    {
        _profileCatalog = profileCatalog;
        _workspace = workspace;
        _folderPicker = folderPicker;
        _changeMonitor = changeMonitor;
        _changeMonitor.RelevantFileChanged += OnRelevantFileChanged;
    }

    public ObservableCollection<ProjectProfileViewModel> Profiles { get; } = [];
    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];
    public ObservableCollection<DiagnosticViewModel> Diagnostics { get; } = [];

    [ObservableProperty]
    private ProjectProfileViewModel? selectedProfile;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Loading project profiles…";

    public string ServiceSummary => $"{_allServices.Count} discovered • {_allServices.Count(service => service.Service.IsEnabled)} enabled";
    public string DiagnosticSummary => Diagnostics.Count == 0 ? "No problems detected" : $"{Diagnostics.Count} item(s) require attention";

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
        _allServices.AddRange(snapshot.Services.Select(service => new ServiceItemViewModel(service, ToggleServiceAsync)));
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
        _changeMonitor.Dispose();
    }
}
