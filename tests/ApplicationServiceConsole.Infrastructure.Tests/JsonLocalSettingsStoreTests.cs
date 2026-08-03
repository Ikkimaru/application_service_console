using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class JsonLocalSettingsStoreTests
{
    [Fact]
    public async Task PersistsOnlyLocalProfileSettings()
    {
        using TemporaryDirectory directory = new();
        using JsonLocalSettingsStore store = new(directory.Path);
        LocalProfileSettings settings = new()
        {
            ProjectRoot = "C:\\Projects\\Example",
            ServiceEnablement = new(StringComparer.OrdinalIgnoreCase) { ["com.example:new"] = true }
        };

        await store.SaveAsync("example", settings);
        LocalProfileSettings loaded = await store.GetAsync("example");

        Assert.Equal(settings.ProjectRoot, loaded.ProjectRoot);
        Assert.True(loaded.ServiceEnablement["com.example:new"]);
    }
}
