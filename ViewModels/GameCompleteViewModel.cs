using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class GameCompleteViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly string _slotName;

    public bool HasOutfitVideo { get; }
    public bool HasSafePhoto { get; }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ICommand ExportOutfitVideoCommand { get; }
    public ICommand ExportSafePhotoCommand { get; }
    public ICommand FinishCommand { get; }

    public GameCompleteViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();
        _slotName = state?.SlotName ?? "unknown";

        var mediaDir = Path.Combine(AppContext.BaseDirectory, "data", "saves", _slotName);
        var videoPath = Path.Combine(mediaDir, "outfit.avi");
        var encPath = Path.Combine(mediaDir, "safe_combo.enc");

        HasOutfitVideo = File.Exists(videoPath);
        HasSafePhoto = File.Exists(encPath);

        ExportOutfitVideoCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var dest = await PickSaveFile("Export Outfit Video", "outfit.avi", ("AVI Video", ["avi"]));
            if (dest is null) return;
            File.Copy(videoPath, dest, overwrite: true);
            StatusMessage = "Outfit video exported.";
        });

        ExportSafePhotoCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var dest = await PickSaveFile("Export Safe Combination", "safe_combo.png", ("PNG Image", ["png"]));
            if (dest is null) return;
            var decrypted = CryptoService.DecryptFile(encPath);
            await File.WriteAllBytesAsync(dest, decrypted);
            StatusMessage = "Safe combination photo exported.";
        });

        FinishCommand = ReactiveCommand.Create(Finish);
    }

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

    private static async Task<string?> PickSaveFile(string title, string defaultName, (string Name, string[] Ext) filter)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices = [new FilePickerFileType(filter.Name) { Patterns = filter.Ext.Select(e => $"*.{e}").ToList() }],
        });

        return file?.TryGetLocalPath();
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
