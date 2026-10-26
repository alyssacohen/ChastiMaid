using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class OutfitRulesEditor : UserControl
{
    public OutfitRulesEditor()
    {
        InitializeComponent();
    }

    // The tag ComboBoxes are editable (free-typed new tags must still work), so Text alone is
    // bound for typing. SelectionChanged additionally captures the full tag string when a
    // suggestion is actually clicked — ignoring the null "deselected" events an editable
    // ComboBox fires while the typed text no longer matches any item, which would otherwise
    // clobber what was just typed.
    private static void OnSourceTagSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: OutfitRuleEditViewModel vm } && e.AddedItems is [string tag])
        {
            vm.SourceTag = tag;
        }
    }

    private static void OnRelatedTagSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: OutfitRuleEditViewModel vm } && e.AddedItems is [string tag])
        {
            vm.RelatedTag = tag;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

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
