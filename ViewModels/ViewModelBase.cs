using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public abstract partial class ViewModelBase : ReactiveObject
{
    [Reactive(SetModifier = AccessModifier.Protected)]
    private ConfirmDialogViewModel? _activeDialog;

    /// <summary>Shows a confirm dialog, awaits its result, then clears it.</summary>
    protected async Task<bool> ShowConfirmAsync(ConfirmDialogViewModel dialog)
    {
        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;
        return confirmed;
    }
}
