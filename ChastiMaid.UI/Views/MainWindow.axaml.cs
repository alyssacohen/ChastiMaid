using Avalonia.Controls;
using ChastiMaid.UI.ViewModels;

namespace ChastiMaid.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext == null)
            DataContext = new MainWindowViewModel();

        PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };
    }

    // Override the closing event
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SaveAndExit(); // Logic we wrote before
        }
        base.OnClosing(e);
    }
}
