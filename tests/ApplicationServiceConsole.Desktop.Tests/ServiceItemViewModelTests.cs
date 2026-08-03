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
        ServiceItemViewModel viewModel = new(service, _ => Task.CompletedTask);

        Assert.Equal("Enable", viewModel.EnableActionText);
        Assert.Equal("Disabled", viewModel.Status);
        Assert.Equal("Auto / unknown", viewModel.PortText);
    }
}
