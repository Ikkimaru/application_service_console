using System.Net;
using System.Net.Sockets;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;
using YamlDotNet.RepresentationModel;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class SpringBootPortResolver : IPortResolver
{
    public Task<PortResolution> ResolveAsync(
        string projectRoot,
        ResolvedService service,
        string springProfile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int? commandPort = ReadCommandPort(service.Command?.Arguments ?? []);
        if (commandPort is not null)
        {
            return Task.FromResult(new PortResolution(commandPort, "command", IsOccupied(commandPort.Value)));
        }

        string resourceRoot = Path.Combine(projectRoot, service.ModulePath.Replace('/', Path.DirectorySeparatorChar), "src", "main", "resources");
        int? profilePort = ReadYamlPort(Path.Combine(resourceRoot, $"application-{springProfile}.yml"))
            ?? ReadYamlPort(Path.Combine(resourceRoot, $"application-{springProfile}.yaml"));
        int? basePort = ReadYamlPort(Path.Combine(resourceRoot, "application.yml"))
            ?? ReadYamlPort(Path.Combine(resourceRoot, "application.yaml"));
        int? port = profilePort ?? basePort;
        return Task.FromResult(new PortResolution(port, profilePort is not null ? $"application-{springProfile}" : "application", port is not null && IsOccupied(port.Value)));
    }

    public int FindAvailablePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static int? ReadCommandPort(IEnumerable<string> arguments)
    {
        foreach (string argument in arguments)
        {
            const string applicationPrefix = "--server.port=";
            const string jvmPrefix = "-Dserver.port=";
            string? value = argument.StartsWith(applicationPrefix, StringComparison.OrdinalIgnoreCase)
                ? argument[applicationPrefix.Length..]
                : argument.StartsWith(jvmPrefix, StringComparison.OrdinalIgnoreCase)
                    ? argument[jvmPrefix.Length..]
                    : null;
            if (int.TryParse(value, out int port) && IsValid(port))
            {
                return port;
            }
        }

        return null;
    }

    private static int? ReadYamlPort(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using StreamReader reader = File.OpenText(path);
            YamlStream yaml = new();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return null;
            }

            if (!root.Children.TryGetValue(new YamlScalarNode("server"), out YamlNode? serverNode)
                || serverNode is not YamlMappingNode server
                || !server.Children.TryGetValue(new YamlScalarNode("port"), out YamlNode? portNode))
            {
                return null;
            }

            return ResolveValue(((YamlScalarNode)portNode).Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    private static int? ResolveValue(string? rawValue)
    {
        if (int.TryParse(rawValue, out int directPort) && IsValid(directPort))
        {
            return directPort;
        }

        if (rawValue is null || !rawValue.StartsWith("${", StringComparison.Ordinal) || !rawValue.EndsWith('}'))
        {
            return null;
        }

        string expression = rawValue[2..^1];
        string[] parts = expression.Split(':', 2);
        string? environmentValue = Environment.GetEnvironmentVariable(parts[0]);
        string? candidate = environmentValue ?? (parts.Length == 2 ? parts[1] : null);
        return int.TryParse(candidate, out int resolvedPort) && IsValid(resolvedPort) ? resolvedPort : null;
    }

    private static bool IsOccupied(int port)
    {
        try
        {
            using TcpListener listener = new(IPAddress.Loopback, port);
            listener.Start();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private static bool IsValid(int port) => port is > IPEndPoint.MinPort and <= IPEndPoint.MaxPort;
}
