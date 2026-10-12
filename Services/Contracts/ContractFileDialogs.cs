using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ChastiMaid.Services.Contracts;

/// <summary>Single-file open/save pickers for contract files, shared by every screen that loads or exports one.</summary>
public static class ContractFileDialogs
{
    /// <summary>The extension every exported contract carries.</summary>
    public const string Extension = ".contract.json";

    /// <summary>Asks for a contract file to load; null when cancelled. Any .json is accepted (it is validated afterwards).</summary>
    public static async Task<string?> PickOpenAsync(string title)
    {
        if (GetTopLevel() is not { } topLevel)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await DataFolderAsync(topLevel),
            FileTypeFilter = [new FilePickerFileType("Contract") { Patterns = ["*.contract.json", "*.json"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <summary>Asks where to save a contract; null when cancelled. The result always ends in <see cref="Extension" />.</summary>
    public static async Task<string?> PickSaveAsync(string title)
    {
        if (GetTopLevel() is not { } topLevel)
        {
            return null;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedStartLocation = await DataFolderAsync(topLevel),
            SuggestedFileName = "chastimaid" + Extension,
            FileTypeChoices = [new FilePickerFileType("Contract") { Patterns = ["*.contract.json"] }],
        });

        return file?.TryGetLocalPath() is { } path ? WithExtension(path) : null;
    }

    /// <summary>Forces the <see cref="Extension" /> onto a chosen path ("a" and "a.json" both become "a.contract.json").</summary>
    public static string WithExtension(string path)
    {
        if (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path[..^".json".Length] : path)
               + Extension;
    }

    /// <summary>The <c>data</c> folder next to the exe, so contracts live with the rest of the app's data wherever it is moved.</summary>
    private static async Task<IStorageFolder?> DataFolderAsync(TopLevel topLevel)
    {
        var dir = Path.Combine(AppPaths.Root, "data");
        Directory.CreateDirectory(dir);
        return await topLevel.StorageProvider.TryGetFolderFromPathAsync(dir);
    }

    private static TopLevel? GetTopLevel() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
