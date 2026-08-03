using System.Reflection;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;
using ApplicationServiceConsole.Infrastructure;

namespace ApplicationServiceConsole.Architecture.Tests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void DomainDoesNotReferenceApplicationOrInfrastructure()
    {
        string[] references = GetReferences(typeof(ProjectProfile).Assembly);
        Assert.DoesNotContain("ApplicationServiceConsole.Application", references);
        Assert.DoesNotContain("ApplicationServiceConsole.Infrastructure", references);
    }

    [Fact]
    public void ApplicationDoesNotReferenceInfrastructure()
    {
        string[] references = GetReferences(typeof(IProfileCatalog).Assembly);
        Assert.DoesNotContain("ApplicationServiceConsole.Infrastructure", references);
    }

    [Fact]
    public void InfrastructureDoesNotReferenceDesktop()
    {
        string[] references = GetReferences(typeof(MavenSpringBootDiscoveryProvider).Assembly);
        Assert.DoesNotContain("ApplicationServiceConsole.Desktop", references);
    }

    private static string[] GetReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray();
}
