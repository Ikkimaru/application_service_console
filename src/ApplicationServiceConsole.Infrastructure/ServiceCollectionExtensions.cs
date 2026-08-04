using ApplicationServiceConsole.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationServiceConsole.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServiceConsole(
        this IServiceCollection services,
        IEnumerable<string> profileDirectories)
    {
        services.AddSingleton<IProfileCatalog>(_ => new JsonProfileCatalog(profileDirectories));
        services.AddSingleton<ILocalSettingsStore, JsonLocalSettingsStore>();
        services.AddSingleton<IServiceDiscoveryProvider, MavenSpringBootDiscoveryProvider>();
        services.AddSingleton<IServiceMetadataProvider, VsCodeLaunchMetadataProvider>();
        services.AddSingleton<IProfileValidator, ProfileValidator>();
        services.AddSingleton<IPortResolver, SpringBootPortResolver>();
        services.AddSingleton<IProfileWorkspace, ProfileWorkspace>();
        services.AddSingleton<IProjectChangeMonitor, ProjectChangeMonitor>();
        services.AddSingleton<IServiceExecutor, LocalProcessServiceExecutor>();
        return services;
    }
}
