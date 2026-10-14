using System.Windows.Input;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Reusable confirm dialog for any confirmation popup in the app.
///     Await Result to get true (confirmed) or false (cancelled).
///     Set CancelLabel = null to hide the Cancel button (OK-only mode).
/// </summary>
public class ConfirmDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConfirmDialogViewModel(
        string title,
        string message,
        string confirmLabel,
        string? cancelLabel,
        string? secondaryLabel = null)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;
        SecondaryLabel = secondaryLabel;
        SecondaryCommand = ReactiveCommand.Create(() =>
        {
            SecondaryChosen = true;
            _tcs.TrySetResult(true);
        });

        ConfirmCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(true));
        CancelCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(false));
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string? CancelLabel { get; }
    public bool HasCancel => CancelLabel is not null;

    /// <summary>Optional middle choice; it completes <see cref="Result" /> with true and sets <see cref="SecondaryChosen" />.</summary>
    public string? SecondaryLabel { get; }
    public bool HasSecondary => SecondaryLabel is not null;
    public bool SecondaryChosen { get; private set; }
    public ICommand SecondaryCommand { get; }

    /// <summary>Cancel gets the focus when the dialog opens (Esc always cancels a dialog that has a Cancel button).</summary>
    public bool CancelIsDefault { get; init; }

    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
}

public enum SaveDialogResult
{
    Discard,
    Save,
    Cancel,
}

/// <summary>
///     3-option dialog: Discard / Save / Cancel.
/// </summary>
public class SaveDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<SaveDialogResult> _tcs = new();

    public SaveDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
        DiscardCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Discard));
        SaveCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Save));
        CancelCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(SaveDialogResult.Cancel));
    }

    public string Title { get; }
    public string Message { get; }

    public Task<SaveDialogResult> Result => _tcs.Task;

    public ICommand DiscardCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
}
