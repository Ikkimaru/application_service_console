using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class SpringBootPortResolverTests
{
    [Fact]
    public async Task ActiveProfilePortOverridesBaseConfiguration()
    {
        using TemporaryDirectory repository = new();
        string resources = Path.Combine(repository.Path, "service", "src", "main", "resources");
        Directory.CreateDirectory(resources);
        await File.WriteAllTextAsync(Path.Combine(resources, "application.yml"), "server:\n  port: 8123\n");
        await File.WriteAllTextAsync(Path.Combine(resources, "application-local.yml"), "server:\n  port: 8124\n");

        PortResolution result = await new SpringBootPortResolver().ResolveAsync(repository.Path, CreateService(), "local");

        Assert.Equal(8124, result.Port);
        Assert.Equal("application-local", result.Source);
    }

    [Fact]
    public async Task CommandPortWinsAndOccupiedPortIsReported()
    {
        using TemporaryDirectory repository = new();
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        ResolvedService service = CreateService() with
        {
            Command = new CommandDefinition
            {
                FileName = "java",
                Arguments = [$"--server.port={port.ToString(CultureInfo.InvariantCulture)}"]
            }
        };

        PortResolution result = await new SpringBootPortResolver().ResolveAsync(repository.Path, service, "local");

        Assert.Equal(port, result.Port);
        Assert.True(result.IsOccupied);
    }

    [Fact]
    public void FindsAnAvailableSessionPort()
    {
        int port = new SpringBootPortResolver().FindAvailablePort();
        Assert.InRange(port, IPEndPoint.MinPort + 1, IPEndPoint.MaxPort);
    }

    private static ResolvedService CreateService() => new()
    {
        Id = "com.example:service",
        DisplayName = "Service",
        Group = "Services",
        ModulePath = "service",
        IsEnabled = true,
        HasStartupMetadata = true,
        Priority = 0,
        Availability = ServiceAvailability.Discovered
    };
}
