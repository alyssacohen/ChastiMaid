using Avalonia.Controls;

namespace ChastiMaid.Views.Dialogs;

/// <summary>
///     Base Window for all modal dialogs in ChastiMaid.
///     Provides <see cref="Confirm" /> and <see cref="Dismiss" /> helpers and
///     a <see cref="DialogResult" /> flag set before closing.
/// </summary>
public class BaseDialog : Window
{
    public bool DialogResult { get; private set; }

    protected void Confirm()
    {
        DialogResult = true;
        Close();
    }

    protected void Dismiss()
    {
        DialogResult = false;
        Close();
    }
}
