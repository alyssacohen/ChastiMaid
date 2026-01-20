using Avalonia.Controls;
using ChastiMaid.UI.ViewModels;

namespace ChastiMaid.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext == null)
            DataContext = new MainViewModel();
    }

    // Override the closing event
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ExitApp(); // Save and exit
        }
        base.OnClosing(e);
    }
}
