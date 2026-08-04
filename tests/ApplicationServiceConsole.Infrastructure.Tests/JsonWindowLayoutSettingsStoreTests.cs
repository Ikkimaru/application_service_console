using ApplicationServiceConsole.Application;

namespace ApplicationServiceConsole.Infrastructure.Tests;

public sealed class JsonWindowLayoutSettingsStoreTests
{
    [Fact]
    public async Task PersistsMachineLocalSidebarWidths()
    {
        using TemporaryDirectory directory = new();
        using JsonWindowLayoutSettingsStore store = new(directory.Path);
        WindowLayoutSettings settings = new(312.5, 428.75);

        await store.SaveAsync(settings);
        WindowLayoutSettings loaded = await store.GetAsync();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task ReturnsDefaultsForMalformedSettings()
    {
        using TemporaryDirectory directory = new();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "window-layout.json"), "not-json");
        using JsonWindowLayoutSettingsStore store = new(directory.Path);

        WindowLayoutSettings loaded = await store.GetAsync();

        Assert.Equal(new WindowLayoutSettings(), loaded);
    }
}
