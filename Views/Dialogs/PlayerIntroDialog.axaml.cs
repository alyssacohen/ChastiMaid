using Avalonia.Controls;
using Avalonia.Threading;

namespace ChastiMaid.Views.Dialogs;

public partial class PlayerIntroDialog : UserControl
{
    public PlayerIntroDialog()
    {
        InitializeComponent();
        // Ready to type as soon as the dialog opens.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not null)
            {
                Dispatcher.UIThread.Post(() => NameBox.Focus());
            }
        };
    }
}
