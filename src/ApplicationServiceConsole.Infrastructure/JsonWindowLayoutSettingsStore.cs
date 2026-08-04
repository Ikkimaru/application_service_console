using System.Text.Json;
using ApplicationServiceConsole.Application;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class JsonWindowLayoutSettingsStore(string? baseDirectory = null) : IWindowLayoutSettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory = baseDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApplicationServiceConsole");

    public async Task<WindowLayoutSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        string path = GetPath();
        if (!File.Exists(path))
        {
            return new();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<WindowLayoutSettings>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(WindowLayoutSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        string path = GetPath();
        string temporaryPath = path + ".tmp";

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private string GetPath() => Path.Combine(_directory, "window-layout.json");
}
