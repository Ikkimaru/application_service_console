using System.IO;

namespace ApplicationServiceConsole.Desktop;

public static class ProfileDirectoryLocator
{
    public static IReadOnlyList<string> Resolve(
        string bundledDirectory,
        string currentDirectory,
        string applicationDataDirectory,
        string applicationBaseDirectory)
    {
        List<string> directories = [Path.GetFullPath(bundledDirectory)];
        AddClosestRepositoryLocalDirectory(directories, currentDirectory);
        AddClosestRepositoryLocalDirectory(directories, applicationBaseDirectory);
        directories.Add(Path.GetFullPath(applicationDataDirectory));
        return directories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddClosestRepositoryLocalDirectory(List<string> directories, string startingDirectory)
    {
        DirectoryInfo? current = new(Path.GetFullPath(startingDirectory));
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "profiles.local");
            if (Directory.Exists(candidate))
            {
                directories.Add(candidate);
                return;
            }

            current = current.Parent;
        }
    }
}
