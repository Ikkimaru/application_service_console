namespace ApplicationServiceConsole.Desktop.Tests;

public sealed class ProfileDirectoryLocatorTests
{
    [Fact]
    public void FindsIgnoredProfilesAboveTheLaunchDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "asc-profile-paths", Guid.NewGuid().ToString("N"));
        string localProfiles = Path.Combine(root, "profiles.local");
        string launchDirectory = Path.Combine(root, "src", "Desktop", "bin", "Debug");
        string bundled = Path.Combine(launchDirectory, "profiles");
        string applicationData = Path.Combine(root, "app-data");
        Directory.CreateDirectory(localProfiles);
        Directory.CreateDirectory(launchDirectory);

        try
        {
            IReadOnlyList<string> directories = ProfileDirectoryLocator.Resolve(
                bundled,
                launchDirectory,
                applicationData,
                launchDirectory);

            Assert.Contains(localProfiles, directories, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.GetFullPath(applicationData), directories[^1]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
