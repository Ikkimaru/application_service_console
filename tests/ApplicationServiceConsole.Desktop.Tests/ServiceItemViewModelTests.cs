using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Desktop.Tests;

public sealed class ServiceItemViewModelTests
{
    [Fact]
    public void DisabledDiscoveredServiceOffersEnablementWithoutExecution()
    {
        ResolvedService service = new()
        {
            Id = "com.example:new-service",
            DisplayName = "New Service",
            Group = "Services",
            ModulePath = "services/new-service",
            IsEnabled = false,
            HasStartupMetadata = false,
            Priority = int.MaxValue,
            Availability = ServiceAvailability.Disabled
        };
        ServiceItemViewModel viewModel = CreateViewModel(service);

        Assert.Equal("Enable", viewModel.EnableActionText);
        Assert.Equal("Disabled", viewModel.Status);
        Assert.Equal("Auto / unknown", viewModel.PortText);
        Assert.False(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public void RunningServiceOffersStopAndRestartButCannotBeDisabledOrStartedAgain()
    {
        ResolvedService service = new()
        {
            Id = "com.example:running-service",
            DisplayName = "Running Service",
            Group = "Services",
            ModulePath = "services/running-service",
            IsEnabled = true,
            HasStartupMetadata = true,
            Priority = 1,
            Availability = ServiceAvailability.Discovered,
            Command = new CommandDefinition { FileName = "example.exe" }
        };
        ServiceItemViewModel viewModel = CreateViewModel(service);

        Assert.True(viewModel.StartCommand.CanExecute(null));

        viewModel.UpdateExecutionState(new(service.Id, ServiceExecutionStatus.Running, 42));

        Assert.Equal("Running", viewModel.Status);
        Assert.False(viewModel.StartCommand.CanExecute(null));
        Assert.True(viewModel.StopCommand.CanExecute(null));
        Assert.True(viewModel.RestartCommand.CanExecute(null));
        Assert.False(viewModel.ToggleEnabledCommand.CanExecute(null));
    }

    private static ServiceItemViewModel CreateViewModel(ResolvedService service) => new(
        service,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        new(true, "Available for testing."),
        new(service.Id, ServiceExecutionStatus.Stopped));
}
