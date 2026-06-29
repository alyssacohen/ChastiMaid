using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.Chores.CollectionChanged += (_, a) => OnItemAdded(a, ChoresScroll);
        vm.Punishments.CollectionChanged += (_, a) => OnItemAdded(a, PunishmentsScroll);
        vm.OutfitRules.CollectionChanged += (_, a) => OnItemAdded(a, RulesScroll);
    }

    private static void OnItemAdded(NotifyCollectionChangedEventArgs args, ScrollViewer scroll)
    {
        if (args.Action is not NotifyCollectionChangedAction.Add)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            scroll.ScrollToEnd();

            Dispatcher.UIThread.Post(() =>
            {
                var lastChild = scroll.GetVisualDescendants()
                    .OfType<Border>()
                    .Where(b => b.Classes.Contains("card"))
                    .LastOrDefault();

                var textBox = lastChild?.GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault();

                textBox?.Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Loaded);
    }
}
