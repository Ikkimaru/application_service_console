using System.Text.Json;
using ApplicationServiceConsole.Application;
using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure;

public sealed class JsonLocalSettingsStore(string? baseDirectory = null) : ILocalSettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory = baseDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApplicationServiceConsole",
        "Profiles");

    public async Task<LocalProfileSettings> GetAsync(string profileId, CancellationToken cancellationToken = default)
    {
        string path = GetPath(profileId);
        if (!File.Exists(path))
        {
            return new();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<LocalProfileSettings>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new();
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

    public async Task SaveAsync(string profileId, LocalProfileSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        string path = GetPath(profileId);
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

    private string GetPath(string profileId)
    {
        string safeName = string.Concat(profileId.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return Path.Combine(_directory, safeName + ".local.json");
    }

    public void Dispose() => _gate.Dispose();
}
