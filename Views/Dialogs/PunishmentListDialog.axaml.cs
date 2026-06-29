using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Dialogs;

public partial class PunishmentListDialog : BaseDialog
{
    public PunishmentListDialog()
    {
        InitializeComponent();
    }

    public PunishmentListDialog(ObservableCollection<PunishmentItemViewModel> punishments) : this()
    {
        DataContext = punishments;
    }

    public PunishmentItemViewModel? SelectedPunishment { get; private set; }

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PunishmentItemViewModel pvm })
        {
            SelectedPunishment = pvm;
            Confirm();
        }
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        Dismiss();
    }
}
