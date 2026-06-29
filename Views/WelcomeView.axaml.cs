using Avalonia.Controls;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        var exitLabel = this.FindControl<TextBlock>("ExitLabel");
        if (exitLabel is null)
        {
            return;
        }

        exitLabel.PointerPressed += (_, _) =>
        {
            if (DataContext is WelcomeViewModel vm)
            {
                vm.ExitCommand.Execute(null);
            }
        };
    }
}
