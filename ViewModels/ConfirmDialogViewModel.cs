using System.Windows.Input;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

/// <summary>
/// Riutilizzabile per qualsiasi popup di conferma nell'app.
/// Await Result per ottenere true (confermato) o false (annullato).
/// Imposta CancelLabel = null per nascondere il pulsante Annulla (solo OK).
/// </summary>
public class ConfirmDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string? CancelLabel { get; }
    public bool HasCancel => CancelLabel is not null;

    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public ConfirmDialogViewModel(
        string title,
        string message,
        string confirmLabel = "Confirm",
        string? cancelLabel = "Cancel")
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;

        ConfirmCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(true));
        CancelCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(false));
    }
}

public enum SaveDialogResult { Discard, Save, Cancel }

/// <summary>
/// 3-option dialog: Discard / Save / Cancel.
/// </summary>
public class SaveDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<SaveDialogResult> _tcs = new();

    public string Title { get; }
    public string Message { get; }

    public Task<SaveDialogResult> Result => _tcs.Task;

    public ICommand DiscardCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SaveDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
        DiscardCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Discard));
        SaveCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Save));
        CancelCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Cancel));
    }
}
