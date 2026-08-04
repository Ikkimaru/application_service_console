using System.IO;
using System.Windows;
using ApplicationServiceConsole.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApplicationServiceConsole.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(e.Args);
            string applicationDataProfiles = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ApplicationServiceConsole",
                "Profiles");
            IReadOnlyList<string> profileDirectories = ProfileDirectoryLocator.Resolve(
                Path.Combine(AppContext.BaseDirectory, "profiles"),
                Environment.CurrentDirectory,
                applicationDataProfiles,
                AppContext.BaseDirectory);
            builder.Services.AddApplicationServiceConsole(profileDirectories);
            builder.Services.AddSingleton<IFolderPicker, FolderPicker>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MainWindow>();
            _host = builder.Build();
            await _host.StartAsync();

            MainWindow window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Application Service Console", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            if (_host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _host.Dispose();
            }
        }

        base.OnExit(e);
    }
}
