using System.Globalization;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Services.Contracts;

namespace ChastiMaid.Tests.Services.Contracts;

public class ContractClauseBuilderTests : IDisposable
{
    // A localizer with no catalog loaded: every text comes out in English, the source language.
    private static readonly ContractClauseBuilder Builder = new(new Localizer());

    private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

    public ContractClauseBuilderTests()
    {
        // Copy is asserted in English regardless of the machine's language.
        CultureInfo.CurrentUICulture = new CultureInfo("en");
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }

    private static GameContract Contract() => new()
    {
        Settings = new GameSettings { Appellatives = "zzfoo, barbaz", MinDebt = 500, MaxDebt = 1000, DailyInterestRate = 0.03 },
        Wardrobe =
        [
            new ClothingItem { Id = 1, Name = "zzdress", Layers = [3] },
            new ClothingItem { Id = 2, Name = "stockings", Layers = [2] },
            new ClothingItem { Id = 3, Name = "shoes", Layers = [4] },
        ],
        OutfitRules =
        [
            new OutfitRule { Id = 1, SourceTag = "a", RelatedTag = "b", Relation = OutfitRelation.Requires },
        ],
        Tasks = [new Chore { Id = 1, Name = "dusting", Value = 5 }],
        ForcedOutfitPresets = [new ForcedOutfitPreset { Id = 1, Name = "uniform", ClothingItemIds = [1] }],
        PlugAndPlayItems = [],
        DiaperPlayItems = [],
        ChastityDeviceItems = [],
        PermissionTypes = [new PermissionTypeConfig { Kind = PermissionKind.Pee, BaseGrantPercent = 50, DurationMinutes = 5, CooldownMinutes = 60 }],
    };

    private static string AllText(IEnumerable<ContractClause> clauses) =>
        string.Join("\n", clauses.SelectMany(c => c.Paragraphs.Prepend(c.Heading)));

    [Fact]
    public void Build_NumbersClausesFromOne_AndEndsWithTheFixedSeverityClause()
    {
        var clauses = Builder.Build(Contract());

        Assert.Equal(Enumerable.Range(1, clauses.Count), clauses.Select(c => c.Number));
        Assert.All(clauses, c => Assert.NotEmpty(c.Paragraphs));
        Assert.Equal("Forms of Address", clauses[0].Title);
        Assert.Equal("Variable Severity", clauses[^1].Title);
        Assert.StartsWith("The severity of punishments is not fixed.", Assert.Single(clauses[^1].Paragraphs));
    }

    [Fact]
    public void Build_SeverityClause_IsPresentAndIdenticalForAnyContract()
    {
        var empty = Builder.Build(new GameContract());
        var full = Builder.Build(Contract());

        Assert.Equal(empty[^1].Title, full[^1].Title);
        Assert.Equal(empty[^1].Paragraphs, full[^1].Paragraphs);
    }

    [Fact]
    public void Build_InterpolatesNumbers_ButNeverTheAppellatives()
    {
        var text = AllText(Builder.Build(Contract()));

        Assert.Contains("£500.00", text);
        Assert.Contains("£1000.00", text);
        Assert.DoesNotContain("zzfoo", text);
        Assert.DoesNotContain("barbaz", text);
        Assert.Contains("keeps 2 forms of address", text); // only their count
    }

    [Fact]
    public void Build_SummarisesTheWardrobePerLayer()
    {
        var text = AllText(Builder.Build(Contract()));

        Assert.Contains("3 ", text);
        Assert.Contains("Base 1", text);
        Assert.Contains("Outerwear 1", text);
        Assert.Contains("Accessories 1", text);
        Assert.DoesNotContain("zzdress", text); // counts, not a list of items
    }

    [Fact]
    public void Build_DisabledFeatures_SayTheyAreNotInForce()
    {
        var contract = Contract();
        contract.Settings!.CornertimeEnabled = false;
        contract.Settings.WritingLinesEnabled = false;
        contract.Settings.KneelEnabled = false;

        var text = AllText(Builder.Build(contract));

        Assert.Contains("Cornertime is not in force.", text);
        Assert.Contains("Lines are not in force.", text);
        Assert.Contains("The Maid shall not be asked to kneel.", text);
        Assert.DoesNotContain("Should the Maid leave the pen idle", text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public void Build_LeavesNoMissingKeysOrPlaceholders_InEitherLanguage(string language)
    {
        var localizer = new Localizer();
        localizer.SetLanguage(language);
        {
            var contract = Contract();
            contract.Settings!.PlugAndPlayEnabled = true;
            contract.Settings.DiaperPlayEnabled = true;
            contract.PlugAndPlayItems = [new PlugAndPlayItemConfig { ClothingItemId = 1, MinDurationMinutes = 30, MaxDurationMinutes = 120 }];
            contract.DiaperPlayItems = [new DiaperPlayItemConfig { ClothingItemId = 2 }];
            contract.ChastityDeviceItems = [new ChastityDeviceItemConfig { ClothingItemId = 3 }];

            var paragraphs = new ContractClauseBuilder(localizer).Build(contract)
                .SelectMany(c => c.Paragraphs.Prepend(c.Title)).ToList();

            Assert.All(paragraphs, p =>
            {
                Assert.DoesNotContain("{", p);
                Assert.False(p.StartsWith("Contract", StringComparison.Ordinal) && !p.Contains(' '), p); // an unresolved key
            });
        }
    }

    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(30, "30 seconds")]
    [InlineData(60, "1 minute")]
    [InlineData(300, "5 minutes")]
    [InlineData(3600, "1 hour")]
    [InlineData(90, "90 seconds")]
    public void Build_FormatsDurationsInTheLargestWholeUnit(int seconds, string expected)
    {
        var contract = Contract();
        contract.Settings!.PopupTimeoutSeconds = seconds;

        var summons = Builder.Build(contract).Single(c => c.Title == "Summons and Check-ins")
            .Paragraphs[0];

        Assert.EndsWith($"within {expected}.", summons);
    }
}
