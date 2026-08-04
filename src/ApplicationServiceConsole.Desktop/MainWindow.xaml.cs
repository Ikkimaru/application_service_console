using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ApplicationServiceConsole.Application;

namespace ApplicationServiceConsole.Desktop;

public partial class MainWindow : Window
{
    private const double DefaultLeftSidebarWidth = 245;
    private const double MinimumLeftSidebarWidth = 180;
    private const double MaximumLeftSidebarWidth = 520;
    private const double DefaultRightSidebarWidth = 350;
    private const double MinimumRightSidebarWidth = 260;
    private const double MaximumRightSidebarWidth = 720;
    private readonly MainViewModel _viewModel;
    private readonly IWindowLayoutSettingsStore _layoutSettingsStore;

    public MainWindow(MainViewModel viewModel, IWindowLayoutSettingsStore layoutSettingsStore)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _layoutSettingsStore = layoutSettingsStore;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        await RestoreLayoutAsync();
        await _viewModel.InitializeAsync();
    }

    private async void OnSidebarDragCompleted(object sender, DragCompletedEventArgs eventArgs) =>
        await SaveLayoutAsync();

    private async Task RestoreLayoutAsync()
    {
        try
        {
            WindowLayoutSettings settings = await _layoutSettingsStore.GetAsync();
            LeftSidebarColumn.Width = new GridLength(Normalize(
                settings.LeftSidebarWidth,
                MinimumLeftSidebarWidth,
                MaximumLeftSidebarWidth,
                DefaultLeftSidebarWidth));
            RightSidebarColumn.Width = new GridLength(Normalize(
                settings.RightSidebarWidth,
                MinimumRightSidebarWidth,
                MaximumRightSidebarWidth,
                DefaultRightSidebarWidth));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LeftSidebarColumn.Width = new GridLength(DefaultLeftSidebarWidth);
            RightSidebarColumn.Width = new GridLength(DefaultRightSidebarWidth);
        }
    }

    private async Task SaveLayoutAsync()
    {
        try
        {
            await _layoutSettingsStore.SaveAsync(new(
                LeftSidebarColumn.ActualWidth,
                RightSidebarColumn.ActualWidth));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Layout persistence is best-effort and must not interrupt service management.
        }
    }

    private static double Normalize(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
