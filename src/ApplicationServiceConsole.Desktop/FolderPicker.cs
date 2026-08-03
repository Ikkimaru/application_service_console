using Microsoft.Win32;

namespace ApplicationServiceConsole.Desktop;

public interface IFolderPicker
{
    string? PickProjectRoot(string? initialDirectory = null);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? PickProjectRoot(string? initialDirectory = null)
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Choose the project repository",
            Multiselect = false,
            InitialDirectory = initialDirectory
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
