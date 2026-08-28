using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ChastiMaid.Localization;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class GameCompleteViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly string _slotName;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string? _statusMessage;

    public GameCompleteViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();
        _slotName = state?.SlotName ?? "unknown";

        var mediaDir = Path.Combine(AppPaths.Root, "data", "saves", _slotName);
        var videoPath = Path.Combine(mediaDir, "outfit.avi");
        var encPath = Path.Combine(mediaDir, "safe_combo.enc");

        HasOutfitVideo = File.Exists(videoPath);
        HasSafePhoto = File.Exists(encPath);

        ExportOutfitVideoCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var dest = await PickSaveFile(Resources.Get("GameCompleteExportVideo"), "outfit.avi", (Resources.Get("GameCompleteAviVideoFilter"), ["avi"]));
            if (dest is null)
            {
                return;
            }

            File.Copy(videoPath, dest, true);
            StatusMessage = Resources.Get("GameCompleteVideoExportedStatus");
        });

        ExportSafePhotoCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var dest = await PickSaveFile(Resources.Get("GameCompleteExportPhotoDialogTitle"), "safe_combo.png", (Resources.Get("GameCompletePngImageFilter"), ["png"]));
            if (dest is null)
            {
                return;
            }

            var decrypted = CryptoService.DecryptFile(encPath);
            await File.WriteAllBytesAsync(dest, decrypted);
            StatusMessage = Resources.Get("GameCompletePhotoExportedStatus");
        });

        FinishCommand = ReactiveCommand.Create(Finish);
    }

    public bool HasOutfitVideo { get; }
    public bool HasSafePhoto { get; }

    public ICommand ExportOutfitVideoCommand { get; }
    public ICommand ExportSafePhotoCommand { get; }
    public ICommand FinishCommand { get; }

    private void Finish()
    {
        var stats = _mainVm.ProfileRepository.GetStats();
        stats.GamesCompleted++;
        _mainVm.ProfileRepository.SaveStats(stats);

        // Close the save DB before deleting the slot
        _mainVm.AutoSaveService.Unbind();
        _mainVm.CloseActiveSave();

        // Delete the slot (DB + media files)
        _mainVm.SaveSlotManager.DeleteSlot(_slotName);

        _mainVm.NavigateTo(new WelcomeViewModel(_mainVm));
    }

    private static async Task<string?> PickSaveFile(string title, string defaultName,
        (string Name, string[] Ext) filter)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return null;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices =
                [new FilePickerFileType(filter.Name) { Patterns = filter.Ext.Select(e => $"*.{e}").ToList() }],
        });

        return file?.TryGetLocalPath();
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
