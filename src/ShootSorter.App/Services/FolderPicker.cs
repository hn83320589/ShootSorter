using Microsoft.Win32;

namespace ShootSorter.App.Services;

public interface IFolderPicker
{
    string? PickFolder(string title, string? initialPath);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string title, string? initialPath)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (System.IO.Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
