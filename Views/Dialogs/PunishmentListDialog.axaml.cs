using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Dialogs;

public partial class PunishmentListDialog : BaseDialog
{
    public PunishmentItemViewModel? SelectedPunishment { get; private set; }

    public PunishmentListDialog()
    {
        InitializeComponent();
    }

    public PunishmentListDialog(ObservableCollection<PunishmentItemViewModel> punishments) : this()
    {
        DataContext = punishments;
    }

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { DataContext: PunishmentItemViewModel pvm })
        {
            SelectedPunishment = pvm;
            Confirm();
        }
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Dismiss();
}
