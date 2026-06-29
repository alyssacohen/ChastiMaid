using Avalonia.Controls;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class WardrobeTable : UserControl
{
    public WardrobeTable()
    {
        InitializeComponent();

        var cb = this.FindControl<CheckBox>("SelectAllCheckBox");
        if (cb is not null)
        {
            cb.IsCheckedChanged += OnSelectAllChanged;
        }
    }

    private void OnSelectAllChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        var selected = (sender as CheckBox)?.IsChecked == true;
        foreach (var item in vm.FilteredWardrobeItems)
        {
            item.IsSelected = selected;
        }
    }
}
