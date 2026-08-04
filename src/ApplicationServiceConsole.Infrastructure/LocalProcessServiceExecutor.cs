using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class LocalProcessServiceExecutor : IServiceExecutor, IAsyncDisposable
{
    private const string StandardOutput = "stdout";
    private const string StandardError = "stderr";
    private readonly ConcurrentDictionary<string, ManagedProcess> _processes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ServiceExecutionState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public ExecutionCapability Capability { get; } = OperatingSystem.IsWindows()
        ? new(true, "Local Windows process execution is available.")
        : new(false, "Local process execution currently requires Windows.");

    public event Action<ServiceExecutionState>? StateChanged;
    public event Action<ServiceOutputLine>? OutputReceived;

    public ServiceExecutionState GetState(string serviceId) =>
        _states.GetValueOrDefault(serviceId) ?? new(serviceId, ServiceExecutionStatus.Stopped);

    public async Task<ExecutionPreflightResult> PreflightAsync(
        ResolvedService service,
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandPlanResult result = BuildCommandPlan(service, projectRoot, null);
        return await Task.FromResult(result.Preflight).ConfigureAwait(false);
    }

    public async Task StartAsync(
        ResolvedService service,
        string projectRoot,
        int? portOverride,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_processes.TryGetValue(service.Id, out ManagedProcess? existing) && !existing.Process.HasExited)
            {
                throw new InvalidOperationException($"Service '{service.DisplayName}' is already running.");
            }

            CommandPlanResult result = BuildCommandPlan(service, projectRoot, portOverride);
            if (result.Plan is null)
            {
                throw new InvalidOperationException(result.Preflight.Message);
            }

            CommandPlan plan = result.Plan;
            Process process = new()
            {
                StartInfo = CreateStartInfo(plan),
                EnableRaisingEvents = false
            };
            ManagedProcess managed = new(process);
            if (!_processes.TryAdd(service.Id, managed))
            {
                process.Dispose();
                throw new InvalidOperationException($"Service '{service.DisplayName}' is already being started.");
            }

            SetState(new(service.Id, ServiceExecutionStatus.Starting));
            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException($"The command for '{service.DisplayName}' did not start.");
                }

                managed.OutputTasks =
                [
                    PumpOutputAsync(service.Id, StandardOutput, process.StandardOutput, plan.SensitiveValues),
                    PumpOutputAsync(service.Id, StandardError, process.StandardError, plan.SensitiveValues)
                ];
                SetState(new(service.Id, ServiceExecutionStatus.Running, process.Id));
                managed.CompletionTask = MonitorExitAsync(service.Id, managed);
            }
            catch
            {
                _processes.TryRemove(service.Id, out _);
                process.Dispose();
                SetState(new(service.Id, ServiceExecutionStatus.Failed, Message: "The process could not be started."));
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        ManagedProcess? managed = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_processes.TryGetValue(serviceId, out managed))
            {
                SetState(new(serviceId, ServiceExecutionStatus.Stopped));
                return;
            }

            managed.StopRequested = true;
            SetState(new(serviceId, ServiceExecutionStatus.Stopping, TryGetProcessId(managed.Process)));
            if (!managed.Process.HasExited)
            {
                managed.Process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the termination request.
        }
        finally
        {
            _gate.Release();
        }

        if (managed?.CompletionTask is not null)
        {
            await managed.CompletionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        string[] serviceIds = _processes.Keys.ToArray();
        foreach (string serviceId in serviceIds)
        {
            await StopAsync(serviceId).ConfigureAwait(false);
        }

        _gate.Dispose();
    }

    private static ProcessStartInfo CreateStartInfo(CommandPlan plan)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = plan.ExecutablePath,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in plan.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string key, string value) in plan.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
    }

    private async Task PumpOutputAsync(
        string serviceId,
        string stream,
        StreamReader reader,
        IReadOnlyList<string> sensitiveValues)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            OutputReceived?.Invoke(new(DateTimeOffset.Now, serviceId, stream, Redact(line, sensitiveValues)));
        }
    }

    private async Task MonitorExitAsync(string serviceId, ManagedProcess managed)
    {
        await managed.Process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(managed.OutputTasks).ConfigureAwait(false);
        int exitCode = managed.Process.ExitCode;
        _processes.TryRemove(serviceId, out _);
        ServiceExecutionStatus status = managed.StopRequested
            ? ServiceExecutionStatus.Stopped
            : exitCode == 0 ? ServiceExecutionStatus.Exited : ServiceExecutionStatus.Failed;
        string? message = status == ServiceExecutionStatus.Failed
            ? $"Process exited with code {exitCode.ToString(CultureInfo.InvariantCulture)}."
            : null;
        SetState(new(serviceId, status, ExitCode: exitCode, Message: message));
        managed.Process.Dispose();
    }

    private void SetState(ServiceExecutionState state)
    {
        _states[state.ServiceId] = state;
        StateChanged?.Invoke(state);
    }

    private CommandPlanResult BuildCommandPlan(ResolvedService service, string projectRoot, int? portOverride)
    {
        if (!Capability.IsAvailable)
        {
            return CommandPlanResult.Failed(Capability.Reason);
        }

        if (!service.IsEnabled)
        {
            return CommandPlanResult.Failed("Enable the service before starting it.");
        }

        if (service.Availability != ServiceAvailability.Discovered)
        {
            return CommandPlanResult.Failed($"Service availability is '{service.Availability}' and cannot be started.");
        }

        if (service.Command is null || string.IsNullOrWhiteSpace(service.Command.FileName))
        {
            return CommandPlanResult.Failed("No local start command is available for this service.");
        }

        string root = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(root))
        {
            return CommandPlanResult.Failed("The configured project folder does not exist.");
        }

        string workingDirectoryValue = Substitute(service.Command.WorkingDirectory ?? root, root);
        string workingDirectory = Path.IsPathRooted(workingDirectoryValue)
            ? Path.GetFullPath(workingDirectoryValue)
            : Path.GetFullPath(Path.Combine(root, workingDirectoryValue));
        if (!IsWithinRoot(root, workingDirectory))
        {
            return CommandPlanResult.Failed("The command working directory must remain inside the configured project folder.");
        }

        if (!Directory.Exists(workingDirectory))
        {
            return CommandPlanResult.Failed($"The command working directory '{workingDirectory}' does not exist.");
        }

        string fileName = Substitute(service.Command.FileName, root);
        string? executablePath = ResolveExecutable(fileName, workingDirectory);
        if (executablePath is null)
        {
            return CommandPlanResult.Failed($"Executable '{fileName}' was not found in the working directory or PATH.");
        }

        List<string> arguments = service.Command.Arguments.Select(argument => Substitute(argument, root)).ToList();
        if (portOverride is not null)
        {
            arguments.Add($"--server.port={portOverride.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        EnvironmentResult environment = LoadEnvironment(service.Command.EnvironmentFile, root);
        if (environment.Error is not null)
        {
            return CommandPlanResult.Failed(environment.Error);
        }

        string[] sensitiveValues = environment.Values.Values
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
        return CommandPlanResult.Succeeded(new(executablePath, workingDirectory, arguments, environment.Values, sensitiveValues));
    }

    private static EnvironmentResult LoadEnvironment(string? configuredPath, string projectRoot)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new(values, null);
        }

        string value = Substitute(configuredPath, projectRoot);
        string path = Path.IsPathRooted(value)
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(projectRoot, value));
        if (!IsWithinRoot(projectRoot, path))
        {
            return new(values, "The environment file must remain inside the configured project folder.");
        }

        if (!File.Exists(path))
        {
            return new(values, $"The configured environment file '{path}' does not exist.");
        }

        foreach (string sourceLine in File.ReadLines(path))
        {
            string line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return new(values, "The environment file contains an invalid entry.");
            }

            string name = line[..separator].Trim();
            if (!IsValidEnvironmentName(name))
            {
                return new(values, "The environment file contains an invalid variable name.");
            }

            string environmentValue = line[(separator + 1)..].Trim();
            if (environmentValue.Length >= 2
                && ((environmentValue[0] == '"' && environmentValue[^1] == '"')
                    || (environmentValue[0] == '\'' && environmentValue[^1] == '\'')))
            {
                environmentValue = environmentValue[1..^1];
            }

            values[name] = environmentValue;
        }

        return new(values, null);
    }

    private static bool IsValidEnvironmentName(string value) =>
        value.Length > 0
        && (char.IsLetter(value[0]) || value[0] == '_')
        && value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string Redact(string value, IReadOnlyList<string> sensitiveValues)
    {
        string result = value;
        foreach (string sensitiveValue in sensitiveValues)
        {
            result = result.Replace(sensitiveValue, "[REDACTED]", StringComparison.Ordinal);
        }

        return result;
    }

    private static string Substitute(string value, string projectRoot) =>
        value.Replace("${projectRoot}", projectRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("${workspaceFolder}", projectRoot, StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinRoot(string projectRoot, string candidate)
    {
        string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(candidate);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveExecutable(string fileName, string workingDirectory)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            string candidate = Path.IsPathRooted(fileName)
                ? Path.GetFullPath(fileName)
                : Path.GetFullPath(Path.Combine(workingDirectory, fileName));
            return File.Exists(candidate) ? candidate : null;
        }

        string[] extensions = Path.HasExtension(fileName)
            ? [string.Empty]
            : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Prepend(string.Empty)
                .ToArray();
        IEnumerable<string> directories = new[] { workingDirectory }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (string directory in directories)
        {
            string cleanDirectory = directory.Trim('"');
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(cleanDirectory, fileName + extension);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private sealed class ManagedProcess(Process process)
    {
        public Process Process { get; } = process;
        public IReadOnlyList<Task> OutputTasks { get; set; } = Array.Empty<Task>();
        public Task? CompletionTask { get; set; }
        public bool StopRequested { get; set; }
    }

    private sealed record CommandPlan(
        string ExecutablePath,
        string WorkingDirectory,
        IReadOnlyList<string> Arguments,
        IReadOnlyDictionary<string, string> EnvironmentVariables,
        IReadOnlyList<string> SensitiveValues);

    private sealed record CommandPlanResult(ExecutionPreflightResult Preflight, CommandPlan? Plan)
    {
        public static CommandPlanResult Failed(string message) => new(new(false, message), null);
        public static CommandPlanResult Succeeded(CommandPlan plan) => new(new(true, "Command is ready to start."), plan);
    }

    private sealed record EnvironmentResult(IReadOnlyDictionary<string, string> Values, string? Error);
}
