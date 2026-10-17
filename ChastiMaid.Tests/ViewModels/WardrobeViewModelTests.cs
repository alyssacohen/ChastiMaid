using System.Collections.ObjectModel;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Tests.ViewModels;

public class WardrobeViewModelTests
{
    private const int Underwear = 0;
    private const int Extra = 1;
    private const int Base = 2;
    private const int Outerwear = 3;

    private readonly ObservableCollection<WardrobeEditViewModel> _items = [];
    private readonly Localizer _l = new();
    private readonly ObservableCollection<OutfitRuleEditViewModel> _rules = [];

    private WardrobeEditViewModel Item(string name, int[] layers, string[]? slots = null, string[]? tags = null,
        double weight = 1, double multiplier = 1) =>
        new(new ClothingItem
        {
            Name = name, Layers = [.. layers], Slots = [.. slots ?? []], Tags = [.. tags ?? []], Weight = weight,
            Multiplier = multiplier,
        }, _ => { }, _l);

    private void Rule(string source, OutfitRelation relation, string related) =>
        _rules.Add(new OutfitRuleEditViewModel(
            new OutfitRule { SourceTag = source, RelatedTag = related, Relation = relation }, [], _ => { }, _l));

    private WardrobeViewModel Create(params WardrobeEditViewModel[] items)
    {
        foreach (var item in items)
        {
            _items.Add(item);
        }

        return new WardrobeViewModel(_items, _rules,
            WardrobeCatalog.Merge(null, items.Select(i => i.ToModel()), _rules.Select(r => r.ToModel())),
            m => new WardrobeEditViewModel(m, _ => { }, _l), _l);
    }

    private static string[] Names(IEnumerable<WardrobeEditViewModel> items) => [.. items.Select(i => i.Name)];

    private static WardrobeChipViewModel Chip(IEnumerable<WardrobeChipViewModel> chips, string label) =>
        chips.Single(c => c.Label == label);

    // ── Search, filters, sort ────────────────────────────────────────────

    [Fact]
    public void Search_MatchesNameLayerSlotAndTag_IgnoringCase()
    {
        var vm = Create(
            Item("apron", [Outerwear], ["torso"], ["maid"]),
            Item("stockings", [Base], ["legs"], ["wool"]),
            Item("corset", [Underwear], ["waist"]));

        vm.SearchText = "APR";
        Assert.Equal(["apron"], Names(vm.VisibleItems));
        vm.SearchText = "under";
        Assert.Equal(["corset"], Names(vm.VisibleItems));
        vm.SearchText = "leg";
        Assert.Equal(["stockings"], Names(vm.VisibleItems));
        vm.SearchText = "maid";
        Assert.Equal(["apron"], Names(vm.VisibleItems));
    }

    [Fact]
    public void Filters_AreOrWithinAGroup_AndAcrossGroups()
    {
        var vm = Create(
            Item("a", [Base], ["legs"]),
            Item("b", [Outerwear], ["torso"]),
            Item("c", [Base, Outerwear], ["feet"]));

        vm.ToggleLayerFilter(Base);
        vm.ToggleLayerFilter(Outerwear);
        Assert.Equal(["a", "b", "c"], Names(vm.VisibleItems));

        vm.ToggleSlotFilter("torso");
        Assert.Equal(["b"], Names(vm.VisibleItems));
        Assert.Equal(3, vm.FilterCount);

        vm.ResetFilters();
        Assert.Equal(3, vm.VisibleItems.Count);
        Assert.Empty(vm.ActiveFilters);
    }

    [Fact]
    public void Sort_SameKeyFlipsDirection_TiesBrokenByName()
    {
        var vm = Create(Item("c", [Base], weight: 2), Item("a", [Base], weight: 1), Item("b", [Base], weight: 2));

        vm.SelectSort(WardrobeSortKey.Weight);
        Assert.Equal(["a", "b", "c"], Names(vm.VisibleItems));

        vm.SelectSort(WardrobeSortKey.Weight);
        Assert.False(vm.SortAscending);
        Assert.Equal(["b", "c", "a"], Names(vm.VisibleItems));

        vm.SelectSort(WardrobeSortKey.Name);
        Assert.True(vm.SortAscending);
        Assert.Equal(["a", "b", "c"], Names(vm.VisibleItems));
    }

    [Fact]
    public void Sort_BySlotAndLayer_PutItemsWithoutValuesFirst()
    {
        var vm = Create(Item("x", [Outerwear], ["torso"]), Item("y", [Base], ["arms"]), Item("z", [Extra]));

        vm.SelectSort(WardrobeSortKey.Slot);
        Assert.Equal(["z", "y", "x"], Names(vm.VisibleItems));

        vm.SelectSort(WardrobeSortKey.Layer);
        Assert.Equal(["z", "y", "x"], Names(vm.VisibleItems));
    }

    // ── Selection ────────────────────────────────────────────────────────

    [Fact]
    public void Targets_AreTheCheckedItems_OrTheActiveOne()
    {
        WardrobeEditViewModel a = Item("a", [Base]), b = Item("b", [Base]), c = Item("c", [Base]);
        var vm = Create(a, b, c);

        vm.Activate(a);
        Assert.Equal([a], vm.Targets);
        Assert.True(a.IsActive);

        b.IsSelected = true;
        c.IsSelected = true;
        Assert.Equal([b, c], vm.Targets);
        Assert.False(a.IsActive);
        Assert.True(vm.IsMultiTarget);

        vm.Activate(c);
        Assert.Equal([c], vm.Targets);
        Assert.False(b.IsSelected);
    }

    [Fact]
    public void SelectionMenu_ActsOnVisibleItems_ClearUnchecksAll()
    {
        WardrobeEditViewModel a = Item("a", [Base]), b = Item("b", [Outerwear]), c = Item("c", [Base]);
        var vm = Create(a, b, c);
        b.IsSelected = true;
        vm.ToggleLayerFilter(Base);

        vm.ToggleAllVisible();
        Assert.True(a.IsSelected && b.IsSelected && c.IsSelected);
        Assert.True(vm.AllVisibleChecked);

        vm.InvertSelection();
        Assert.False(a.IsSelected || c.IsSelected);
        Assert.True(b.IsSelected); // hidden: untouched

        a.IsSelected = true;
        Assert.Null(vm.AllVisibleChecked);

        vm.ClearSelection();
        Assert.DoesNotContain(_items, i => i.IsSelected);
    }

    // ── Tri-state chips ──────────────────────────────────────────────────

    [Fact]
    public void Chips_ShowFullPartialAndNone_AcrossTargets()
    {
        WardrobeEditViewModel a = Item("a", [Base], ["legs"], ["maid", "wool"]), b = Item("b", [Base], [], ["maid"]);
        var vm = Create(a, b);
        a.IsSelected = b.IsSelected = true;

        Assert.True(Chip(vm.TagChips, "maid").IsChecked);
        Assert.Null(Chip(vm.TagChips, "wool").IsChecked);
        Assert.Equal("1/2", Chip(vm.TagChips, "wool").CountText);
        Assert.False(Chip(vm.SlotChips, "legs").IsChecked is true);
        Assert.True(Chip(vm.LayerChips, "Base").IsChecked);
        Assert.False(Chip(vm.LayerChips, "Extra").IsChecked);
    }

    [Fact]
    public void Chip_PartialAddsToAllTargets_FullRemovesFromAll()
    {
        WardrobeEditViewModel a = Item("a", [Base], tags: ["maid", "wool"]), b = Item("b", [Base], tags: ["maid"]);
        var vm = Create(a, b, Item("other", [Base], tags: ["wool"]));
        a.IsSelected = b.IsSelected = true;

        Chip(vm.TagChips, "wool").Toggle();
        Assert.Equal(["maid", "wool"], b.Tags);
        Assert.True(Chip(vm.TagChips, "wool").IsChecked);

        Chip(vm.TagChips, "wool").Toggle();
        Assert.DoesNotContain("wool", a.Tags);
        Assert.DoesNotContain("wool", b.Tags);
        Assert.Null(vm.Confirmation); // "other" still uses it
    }

    [Fact]
    public void LayerChip_AddsInLayerOrder_AndNeverRemovesTheLastLayer()
    {
        WardrobeEditViewModel a = Item("a", [Outerwear]), b = Item("b", [Base, Outerwear]);
        var vm = Create(a, b);
        a.IsSelected = b.IsSelected = true;

        Chip(vm.LayerChips, "Base").Toggle();
        Assert.Equal([Base, Outerwear], a.Layers);

        Chip(vm.LayerChips, "Outerwear").Toggle();
        Assert.Equal([Base], a.Layers);

        Chip(vm.LayerChips, "Base").Toggle();
        Assert.Equal([Base], a.Layers);
        Assert.NotNull(vm.LayerError);
    }

    // ── Slots and tags ───────────────────────────────────────────────────

    [Fact]
    public void RemovingTheLastUser_AsksToDeleteTheEntry_WithItsRules()
    {
        var a = Item("a", [Base], tags: ["lace", "maid"]);
        Rule("lace", OutfitRelation.Requires, "maid");
        var vm = Create(a, Item("b", [Base], tags: ["maid"]));
        vm.Activate(a);

        Chip(vm.TagChips, "lace").Toggle();
        Assert.NotNull(vm.Confirmation);
        Assert.Contains("rule", vm.Confirmation!.Message);

        vm.Confirmation.ConfirmCommand.Execute(null);
        Assert.Null(vm.Confirmation);
        Assert.DoesNotContain("lace", vm.Tags);
        Assert.Empty(_rules);
        Assert.NotNull(vm.Notice);
    }

    [Fact]
    public void KeepingAnUnusedEntry_LeavesItInTheList()
    {
        var a = Item("a", [Base], ["legs"]);
        var vm = Create(a);
        vm.Activate(a);

        Chip(vm.SlotChips, "legs").Toggle();
        vm.Confirmation!.CancelCommand.Execute(null);

        Assert.Contains("legs", vm.Slots);
        Assert.Empty(a.Slots);
    }

    [Fact]
    public void AddEntry_RefusesEmptyAndDuplicateNames_AppliesToTargetsOnlyWhenAsked()
    {
        var a = Item("a", [Base], tags: ["maid"]);
        var vm = Create(a);
        vm.Activate(a);

        Assert.NotNull(vm.AddEntry(WardrobeEntryKind.Tag, "  ", true));
        Assert.NotNull(vm.AddEntry(WardrobeEntryKind.Tag, " MAID ", true));

        Assert.Null(vm.AddEntry(WardrobeEntryKind.Tag, "cotton", true));
        Assert.Equal(["cotton", "maid"], a.Tags);

        Assert.Null(vm.AddEntry(WardrobeEntryKind.Slot, "neck", false));
        Assert.Contains("neck", vm.Slots);
        Assert.Empty(a.Slots);
        Assert.False(Chip(vm.SlotChips, "neck").IsChecked);
    }

    [Fact]
    public void DeletingAnEntry_IsAlwaysConfirmed_AndCascadesToItemsFiltersAndRules()
    {
        Rule("winter", OutfitRelation.Incompatible, "summer");
        var vm = Create(Item("a", [Base], tags: ["winter"]), Item("b", [Base], tags: ["summer"]));
        vm.ToggleTagFilter("winter");
        vm.OpenManage(WardrobeEntryKind.Tag);
        Assert.Equal("1 item · 1 rule", vm.ManageRows.Single(r => r.Name == "winter").Usage);

        vm.AskDeleteEntry(WardrobeEntryKind.Tag, "winter");
        vm.Confirmation!.ConfirmCommand.Execute(null);

        Assert.DoesNotContain(_items, i => i.Tags.Contains("winter"));
        Assert.DoesNotContain("winter", vm.Tags);
        Assert.Equal(0, vm.FilterCount);
        Assert.Empty(_rules);
        Assert.DoesNotContain(vm.ManageRows, r => r.Name == "winter");
    }

    // ── Weight and Multiplier ────────────────────────────────────────────

    [Fact]
    public void NumberField_ShowsTheSharedValue_OrNothingWithTheRange()
    {
        WardrobeEditViewModel a = Item("a", [Base], weight: 0.5), b = Item("b", [Base], weight: 1);
        var vm = Create(a, b);

        vm.Activate(a);
        Assert.Equal(0.5m, vm.Weight.Value);
        Assert.False(vm.Weight.IsMixed);

        a.IsSelected = b.IsSelected = true;
        Assert.Null(vm.Weight.Value);
        Assert.True(vm.Weight.IsMixed);
        Assert.Equal("from 0.5 to 1.0", vm.Weight.RangeText);
        Assert.Equal(1m, vm.Multiplier.Value);
    }

    [Fact]
    public void NumberField_WritesValidNumbersToAllTargets_IgnoresEmptyAndNegative()
    {
        WardrobeEditViewModel a = Item("a", [Base], weight: 0.5), b = Item("b", [Base], weight: 1);
        var vm = Create(a, b);
        a.IsSelected = b.IsSelected = true;

        vm.Weight.Value = null;
        vm.Weight.Value = -1;
        Assert.Equal([0.5, 1], new[] { a.Weight, b.Weight });

        vm.Weight.Value = 2;
        Assert.Equal([2.0, 2.0], new[] { a.Weight, b.Weight });
        Assert.False(vm.Weight.IsMixed);
    }

    [Fact]
    public void NumberField_StepsEachTargetOnItsOwn_NeverBelowZero()
    {
        WardrobeEditViewModel a = Item("a", [Base], multiplier: 0.05), b = Item("b", [Base], multiplier: 1.2);
        var vm = Create(a, b);
        a.IsSelected = b.IsSelected = true;

        vm.Multiplier.StepBy(-WardrobeNumberFieldViewModel.Step);
        Assert.Equal([0.0, 1.1], new[] { a.Multiplier, b.Multiplier });
        Assert.Null(vm.Multiplier.Value);

        vm.Multiplier.StepBy(WardrobeNumberFieldViewModel.Step);
        Assert.Equal([0.1, 1.2], new[] { a.Multiplier, b.Multiplier });
    }

    // ── New item, rename ─────────────────────────────────────────────────

    [Fact]
    public void AddItem_UsesTheSingleLayerFilter_ResetsFilters_AndStartsRenaming()
    {
        var vm = Create(Item("a", [Base]));
        vm.ToggleLayerFilter(Outerwear);
        vm.SearchText = "zzz";

        vm.AddItem();

        var added = _items.Last();
        Assert.Equal([Outerwear], added.Layers);
        Assert.Equal(1, added.Weight);
        Assert.Equal(1, added.Multiplier);
        Assert.Equal("", vm.SearchText);
        Assert.Equal(0, vm.FilterCount);
        Assert.Contains(added, vm.VisibleItems);
        Assert.Same(added, vm.ActiveItem);
        Assert.Same(added, vm.EditingItem);

        vm.AddItem();
        Assert.Equal([Base], _items.Last().Layers);
    }

    [Fact]
    public void Rename_RefusesAnExistingName_AndKeepsEditing()
    {
        var vm = Create(Item("Apron", [Outerwear]), Item("dress", [Outerwear]));
        var rejected = 0;
        vm.RenameRejected += (_, _) => rejected++;
        var dress = _items[1];

        vm.BeginRename(dress);
        vm.EditText = " apron ";
        Assert.False(vm.CommitRename());
        Assert.Equal(1, rejected);
        Assert.Same(dress, vm.EditingItem);
        Assert.NotNull(vm.RenameError);

        vm.EditText = "  ";
        Assert.True(vm.CommitRename());
        Assert.Equal("dress", dress.Name);

        vm.BeginRename(dress);
        vm.EditText = "Dress";
        Assert.True(vm.CommitRename());
        Assert.Equal("Dress", dress.Name);
    }

    [Fact]
    public void NewItem_WithATakenDefaultName_CannotBeCommitted_AndEscapeUndoesIt()
    {
        var vm = Create(Item("New item", [Base]));

        vm.AddItem();
        Assert.False(vm.CommitRename());
        Assert.Equal(2, _items.Count);

        vm.CancelRename();
        Assert.Single(_items);
        Assert.Null(vm.EditingItem);
    }

    // ── Delete and undo ──────────────────────────────────────────────────

    [Fact]
    public void DeleteTargets_ThenUndo_PutsTheItemsBackInPlace()
    {
        WardrobeEditViewModel a = Item("a", [Base]), b = Item("b", [Base]), c = Item("c", [Base]);
        var vm = Create(a, b, c);
        a.IsSelected = c.IsSelected = true;

        vm.DeleteTargets();
        Assert.Equal([b], _items);
        Assert.True(vm.CanUndo);
        Assert.Equal("2 items deleted", vm.Notice);
        Assert.False(vm.HasTargets);

        vm.UndoDelete();
        Assert.Equal([a, b, c], _items);
        Assert.Null(vm.Notice);
        Assert.False(a.IsSelected);
    }

    // ── Rules of the item ────────────────────────────────────────────────

    [Fact]
    public void ItemRules_GroupRequires_AndListIncompatibleFromEitherTag()
    {
        Rule("garter-belt", OutfitRelation.Requires, "stockings");
        Rule("garter-belt", OutfitRelation.Requires, "tights");
        Rule("lace", OutfitRelation.Requires, "maid");
        Rule("summer", OutfitRelation.Incompatible, "winter");
        var belt = Item("belt", [Underwear], tags: ["Garter-Belt", "winter"]);
        var vm = Create(belt, Item("other", [Base]));

        vm.Activate(belt);

        Assert.Equal(["garter-belt requires one of: stockings, tights.", "winter excludes summer"], vm.ItemRules);
        Assert.True(vm.ShowItemRules);

        _items[1].IsSelected = belt.IsSelected = true;
        Assert.Empty(vm.ItemRules);
        Assert.False(vm.ShowItemRules);
    }

    // ── Saving ───────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeForSave_LowersSlotsAndTagsEverywhere_AndSortsLayers()
    {
        var a = Item("a", [Outerwear, Base], ["Legs", "legs"], ["Maid"]);
        Rule("Maid", OutfitRelation.Requires, " Lace ");
        var vm = Create(a);
        vm.AddEntry(WardrobeEntryKind.Slot, "Neck", false);

        var catalog = vm.NormalizeForSave();

        Assert.Equal(["legs"], a.Slots);
        Assert.Equal(["maid"], a.Tags);
        Assert.Equal([Base, Outerwear], a.Layers);
        Assert.Equal(["legs", "neck"], catalog.Slots);
        Assert.Equal(["lace", "maid"], catalog.Tags);
        Assert.Equal(["lace", "maid"], vm.Tags);
        Assert.Equal(("maid", "lace"), (_rules[0].SourceTag, _rules[0].RelatedTag));
    }

    [Fact]
    public void CatalogMerge_KeepsUnusedEntries_AndAddsEverythingInUse()
    {
        var catalog = WardrobeCatalog.Merge(
            new WardrobeCatalog { Slots = ["neck", " "], Tags = ["unused"] },
            [new ClothingItem { Slots = ["Legs", "legs"], Tags = ["maid"] }],
            [new OutfitRule { SourceTag = "maid", RelatedTag = "lace" }]);

        Assert.Equal(["Legs", "neck"], catalog.Slots);
        Assert.Equal(["lace", "maid", "unused"], catalog.Tags);
    }
}
