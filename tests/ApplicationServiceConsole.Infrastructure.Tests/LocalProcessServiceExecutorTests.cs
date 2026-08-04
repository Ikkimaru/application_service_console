using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class LocalProcessServiceExecutorTests
{
    [Fact]
    public async Task PreflightRejectsWorkingDirectoryOutsideProjectRoot()
    {
        using TemporaryDirectory project = new();
        await using LocalProcessServiceExecutor executor = new();
        ResolvedService service = CreateService(new()
        {
            FileName = "cmd.exe",
            WorkingDirectory = Path.GetDirectoryName(project.Path)
        });

        ExecutionPreflightResult result = await executor.PreflightAsync(service, project.Path);

        Assert.False(result.CanStart);
        Assert.Contains("inside", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartsCommandAndCapturesOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory project = new();
        await using LocalProcessServiceExecutor executor = new();
        ResolvedService service = CreateService(new()
        {
            FileName = "cmd.exe",
            WorkingDirectory = "${projectRoot}",
            Arguments = ["/d", "/s", "/c", "echo executor-ready"]
        });
        List<ServiceOutputLine> output = [];
        TaskCompletionSource<ServiceExecutionState> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        executor.OutputReceived += output.Add;
        executor.StateChanged += state =>
        {
            if (state.Status is ServiceExecutionStatus.Exited or ServiceExecutionStatus.Failed)
            {
                completion.TrySetResult(state);
            }
        };

        await executor.StartAsync(service, project.Path, null);
        ServiceExecutionState finalState = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ServiceExecutionStatus.Exited, finalState.Status);
        Assert.Equal(0, finalState.ExitCode);
        Assert.Contains(output, line => line.Message.Contains("executor-ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopsRunningProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory project = new();
        await using LocalProcessServiceExecutor executor = new();
        ResolvedService service = CreateService(new()
        {
            FileName = "powershell.exe",
            WorkingDirectory = "${projectRoot}",
            Arguments = ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"]
        });

        await executor.StartAsync(service, project.Path, null);
        await executor.StopAsync(service.Id);

        Assert.Equal(ServiceExecutionStatus.Stopped, executor.GetState(service.Id).Status);
    }

    [Fact]
    public async Task RedactsEnvironmentValuesFromCapturedOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory project = new();
        string environmentFile = Path.Combine(project.Path, ".env");
        await File.WriteAllTextAsync(environmentFile, "SERVICE_TOKEN=local-test-secret");
        await using LocalProcessServiceExecutor executor = new();
        ResolvedService service = CreateService(new()
        {
            FileName = "cmd.exe",
            WorkingDirectory = "${projectRoot}",
            EnvironmentFile = ".env",
            Arguments = ["/d", "/s", "/c", "echo %SERVICE_TOKEN%"]
        });
        List<ServiceOutputLine> output = [];
        TaskCompletionSource<ServiceExecutionState> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        executor.OutputReceived += output.Add;
        executor.StateChanged += state =>
        {
            if (state.Status is ServiceExecutionStatus.Exited or ServiceExecutionStatus.Failed)
            {
                completion.TrySetResult(state);
            }
        };

        await executor.StartAsync(service, project.Path, null);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains(output, line => line.Message.Contains("[REDACTED]", StringComparison.Ordinal));
        Assert.DoesNotContain(output, line => line.Message.Contains("local-test-secret", StringComparison.Ordinal));
    }

    private static ResolvedService CreateService(CommandDefinition command) => new()
    {
        Id = "com.example:service",
        DisplayName = "Example Service",
        Group = "Services",
        ModulePath = "service",
        IsEnabled = true,
        HasStartupMetadata = true,
        Priority = 1,
        Availability = ServiceAvailability.Discovered,
        Command = command
    };
}
