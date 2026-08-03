using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure;

public sealed partial class MavenSpringBootDiscoveryProvider : IServiceDiscoveryProvider
{
    public string Type => "mavenSpringBoot";

    public Task<ServiceDiscoveryResult> DiscoverAsync(
        string projectRoot,
        ProviderDefinition configuration,
        string springProfile,
        CancellationToken cancellationToken = default)
    {
        string searchRoot = ResolveSearchRoot(projectRoot, configuration.Options.GetValueOrDefault("searchRoot", "."));
        List<DiscoveredService> services = [];
        List<ServiceDiagnostic> diagnostics = [];

        if (!Directory.Exists(searchRoot))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "DISCOVERY_ROOT_MISSING", $"Discovery root '{searchRoot}' does not exist."));
            return Task.FromResult(new ServiceDiscoveryResult(services, diagnostics));
        }

        foreach (string pomPath in EnumerateReactorPomFiles(searchRoot, diagnostics))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDiscoverModule(projectRoot, pomPath, springProfile, services, diagnostics);
        }

        return Task.FromResult(new ServiceDiscoveryResult(services, diagnostics));
    }

    private static List<string> EnumerateReactorPomFiles(string root, List<ServiceDiagnostic> diagnostics)
    {
        string rootPom = Path.Combine(root, "pom.xml");
        if (!File.Exists(rootPom))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "MAVEN_ROOT_POM_MISSING", $"No pom.xml exists at discovery root '{root}'."));
            return [];
        }

        List<string> result = [];
        Queue<string> pending = new();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(rootPom);

        while (pending.TryDequeue(out string? pomPath))
        {
            string fullPath = Path.GetFullPath(pomPath);
            if (!visited.Add(fullPath))
            {
                continue;
            }

            if (!File.Exists(fullPath))
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "MAVEN_MODULE_MISSING", $"Declared Maven module '{fullPath}' has no pom.xml."));
                continue;
            }

            result.Add(fullPath);
            try
            {
                XDocument document = XDocument.Load(fullPath, LoadOptions.None);
                XElement? project = document.Root;
                if (project is null)
                {
                    continue;
                }

                XNamespace xmlNamespace = project.Name.Namespace;
                IEnumerable<string> modules = project.Element(xmlNamespace + "modules")?
                    .Elements(xmlNamespace + "module")
                    .Select(module => module.Value.Trim()) ?? [];
                string parentDirectory = Path.GetDirectoryName(fullPath)!;
                foreach (string module in modules.Where(module => module.Length > 0))
                {
                    string modulePom = Path.GetFullPath(Path.Combine(parentDirectory, module, "pom.xml"));
                    if (modulePom.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue(modulePom);
                    }
                    else
                    {
                        diagnostics.Add(new(DiagnosticSeverity.Warning, "MAVEN_MODULE_OUTSIDE_ROOT", $"Module '{module}' is outside the discovery root."));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "MAVEN_POM_INVALID", $"Could not traverse '{fullPath}': {exception.Message}"));
            }
        }

        return result;
    }

    private static void TryDiscoverModule(
        string projectRoot,
        string pomPath,
        string springProfile,
        List<DiscoveredService> services,
        List<ServiceDiagnostic> diagnostics)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(pomPath, LoadOptions.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            diagnostics.Add(new(DiagnosticSeverity.Warning, "MAVEN_POM_INVALID", $"Could not read '{Path.GetRelativePath(projectRoot, pomPath)}': {exception.Message}"));
            return;
        }

        XElement? project = document.Root;
        if (project is null)
        {
            return;
        }

        XNamespace xmlNamespace = project.Name.Namespace;
        string? artifactId = project.Element(xmlNamespace + "artifactId")?.Value.Trim();
        string? groupId = project.Element(xmlNamespace + "groupId")?.Value.Trim()
            ?? project.Element(xmlNamespace + "parent")?.Element(xmlNamespace + "groupId")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(artifactId))
        {
            diagnostics.Add(new(DiagnosticSeverity.Warning, "MAVEN_COORDINATES_MISSING", $"Maven coordinates are incomplete in '{Path.GetRelativePath(projectRoot, pomPath)}'."));
            return;
        }

        string moduleDirectory = Path.GetDirectoryName(pomPath)!;
        string javaRoot = Path.Combine(moduleDirectory, "src", "main", "java");
        if (!Directory.Exists(javaRoot))
        {
            return;
        }

        List<string> entryPoints = [];
        foreach (string sourcePath in Directory.EnumerateFiles(javaRoot, "*.java", SearchOption.AllDirectories))
        {
            string source;
            try
            {
                source = File.ReadAllText(sourcePath);
            }
            catch (IOException)
            {
                continue;
            }

            if (!SpringBootApplicationRegex().IsMatch(source))
            {
                continue;
            }

            Match classMatch = ClassRegex().Match(source);
            if (!classMatch.Success)
            {
                continue;
            }

            Match packageMatch = PackageRegex().Match(source);
            string className = classMatch.Groups[1].Value;
            entryPoints.Add(packageMatch.Success ? $"{packageMatch.Groups[1].Value}.{className}" : className);
        }

        if (entryPoints.Count == 0)
        {
            return;
        }

        string modulePath = Path.GetRelativePath(projectRoot, moduleDirectory).Replace('\\', '/');
        string id = $"{groupId}:{artifactId}";
        services.Add(new()
        {
            Id = id,
            GroupId = groupId,
            ArtifactId = artifactId,
            ModulePath = modulePath,
            ApplicationEntryPoints = entryPoints.Order(StringComparer.Ordinal).ToArray(),
            SuggestedCommand = new()
            {
                FileName = "mvn.cmd",
                WorkingDirectory = "${projectRoot}",
                Arguments = ["-pl", modulePath, "-am", "spring-boot:run", "-P", springProfile, $"-Dspring-boot.run.profiles={springProfile}"]
            }
        });
    }

    private static string ResolveSearchRoot(string projectRoot, string relativePath)
    {
        string root = Path.GetFullPath(projectRoot);
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Discovery searchRoot must remain inside the configured project root.");
        }

        return candidate;
    }

    [GeneratedRegex("@SpringBootApplication\\b", RegexOptions.CultureInvariant)]
    private static partial Regex SpringBootApplicationRegex();

    [GeneratedRegex("\\bclass\\s+([A-Za-z_$][A-Za-z0-9_$]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ClassRegex();

    [GeneratedRegex("\\bpackage\\s+([A-Za-z_$][A-Za-z0-9_$.]*)\\s*;", RegexOptions.CultureInvariant)]
    private static partial Regex PackageRegex();
}
