using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services.Contracts;
using LiteDB;

namespace ChastiMaid.Tests.Services.Contracts;

public class GameContractServiceTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly ProfileRepository _profile;
    private readonly GameContractService _service;

    public GameContractServiceTests()
    {
        _profile = new ProfileRepository(_db);
        _service = new GameContractService(_profile);
        _profile.ReplaceWardrobe(
        [
            new ClothingItem { Id = 1, Name = "dress", Layers = [3], Slots = ["torso"], Tags = ["dress"] },
            new ClothingItem { Id = 2, Name = "stockings", Layers = [1], Slots = ["legs"], Tags = ["stockings"] },
        ]);
        _profile.ReplaceOutfitRules(
        [
            new OutfitRule { Id = 1, SourceTag = "dress", RelatedTag = "stockings", Relation = OutfitRelation.Requires },
        ]);
        _profile.ReplaceChores([new Chore { Id = 1, Name = "dusting", Value = 5, FrequencyDays = 2, MinCompletionMinutes = 10 }]);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private GameContract ValidContract() => _service.Build();

    [Fact]
    public void Build_IncludesSettingsWardrobeRulesAndTasks()
    {
        var contract = _service.Build();

        Assert.Equal(GameContract.CurrentSchemaVersion, contract.SchemaVersion);
        Assert.NotNull(contract.Settings);
        Assert.Equal(2, contract.Wardrobe!.Count);
        Assert.Single(contract.OutfitRules!);
        Assert.Single(contract.Tasks!);
    }

    [Fact]
    public void Serialize_ExcludesDeviceLevelSettings()
    {
        var contract = _service.Build();
        contract.Settings!.PlayerName = "Secret";
        contract.Settings.SelectedCameraIndex = 3;

        var json = _service.Serialize(contract);

        Assert.DoesNotContain("Secret", json);
        using var doc = JsonDocument.Parse(json);
        var settings = doc.RootElement.GetProperty("settings");
        foreach (var name in new[]
                 {
                     "id", "playerName", "autoSaveIntervalMinutes", "appellatives", "language",
                     "cornertimeVoiceEnabled", "selectedCameraIndex",
                 })
        {
            Assert.False(settings.TryGetProperty(name, out _), name);
        }

        Assert.True(settings.TryGetProperty("idleWageAmount", out _));
    }

    [Fact]
    public void GameSettings_DeviceLevelProperties_AreExactlyTheJsonIgnoredOnes()
    {
        var ignored = typeof(GameSettings).GetProperties()
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            .Select(p => p.Name)
            .Order();

        Assert.Equal(
            new[]
            {
                nameof(GameSettings.Appellatives), nameof(GameSettings.AutoSaveIntervalMinutes),
                nameof(GameSettings.CornertimeVoiceEnabled), nameof(GameSettings.Id),
                nameof(GameSettings.Language), nameof(GameSettings.PlayerName),
                nameof(GameSettings.SelectedCameraIndex),
            }.Order(),
            ignored);
    }

    [Fact]
    public void RangedProperties_CoverEveryNumericGameRuleSetting()
    {
        var numeric = typeof(GameSettings).GetProperties()
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null
                        && p.PropertyType != typeof(bool) && p.PropertyType != typeof(string)
                        && p.CanWrite)
            .Select(p => p.Name);

        Assert.All(numeric, name => Assert.Contains(name, GameContractService.RangedProperties));
    }

    [Fact]
    public void RoundTrip_ValidateThenApply_RestoresEqualContent()
    {
        var json = _service.Serialize(_service.Build());
        using var otherDb = new LiteDatabase(":memory:");
        var otherProfile = new ProfileRepository(otherDb);
        var other = new GameContractService(otherProfile);

        var result = other.Validate(json);
        other.Apply(result);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(_profile.GetWardrobe().Select(i => i.Name), otherProfile.GetWardrobe().Select(i => i.Name));
        Assert.Equal(_profile.GetOutfitRules().Count, otherProfile.GetOutfitRules().Count);
        Assert.Equal(_profile.GetChores().Select(c => c.Name), otherProfile.GetChores().Select(c => c.Name));
        Assert.Equal(_profile.GetGameSettings().IdleWageAmount, otherProfile.GetGameSettings().IdleWageAmount);
    }

    [Fact]
    public void Validate_InvalidJson_IsFatal()
    {
        var result = _service.Validate("{ not json");

        Assert.False(result.IsValid);
        Assert.Equal(ContractIssueKind.InvalidFile, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public void Validate_WrongSectionType_IsFatal()
    {
        var result = _service.Validate("""{"schemaVersion":1,"settings":{},"wardrobe":5}""");

        Assert.False(result.IsValid);
        Assert.Equal(ContractIssueKind.InvalidFile, Assert.Single(result.Issues).Kind);
    }

    [Theory]
    [InlineData("""{"settings":{}}""")]
    [InlineData("""{"schemaVersion":"1"}""")]
    [InlineData("[]")]
    public void Validate_MissingOrMalformedSchemaVersion_IsFatal(string json)
    {
        var result = _service.Validate(json);

        Assert.False(result.IsValid);
        Assert.Equal(ContractIssueKind.InvalidFile, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public void Validate_UnknownSchemaVersion_IsFatal()
    {
        var result = _service.Validate("""{"schemaVersion":99,"anything":"else"}""");

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ContractIssueKind.UnsupportedSchemaVersion, issue.Kind);
        Assert.Equal("99", issue.Subject);
    }

    [Fact]
    public void Validate_MissingSections_AreAllReported()
    {
        var result = _service.Validate("""{"schemaVersion":1}""");

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Issues.Count(i => i.Kind == ContractIssueKind.MissingSection));
    }

    [Fact]
    public void Validate_OutOfRangeValues_AreClampedAndReported()
    {
        var contract = ValidContract();
        contract.Settings!.IdleWageIntervalMinutes = 5_000;
        contract.Settings.QuoteMatchMinSimilarityPercent = -3;
        contract.Settings.DailyInterestRate = 7;

        var result = _service.Validate(contract);

        Assert.True(result.IsValid);
        Assert.Equal(120, result.Contract!.Settings!.IdleWageIntervalMinutes);
        Assert.Equal(1, result.Contract.Settings.QuoteMatchMinSimilarityPercent);
        Assert.Equal(0.10, result.Contract.Settings.DailyInterestRate);
        Assert.Contains(result.Issues, i => i is { Kind: ContractIssueKind.ValueClamped, Subject: "IdleWageIntervalMinutes" });
        Assert.Equal(3, result.Issues.Count(i => i.Kind == ContractIssueKind.ValueClamped));
    }

    [Fact]
    public void Validate_MinGreaterThanMax_IsSwappedAndReported()
    {
        var contract = ValidContract();
        contract.Settings!.MinDebt = 900;
        contract.Settings.MaxDebt = 400;

        var result = _service.Validate(contract);

        Assert.Equal(400, result.Contract!.Settings!.MinDebt);
        Assert.Equal(900, result.Contract.Settings.MaxDebt);
        Assert.Contains(result.Issues, i => i is { Kind: ContractIssueKind.RangeSwapped, Subject: "MinDebt" });
    }

    [Fact]
    public void Validate_RuleReferencingMissingTags_IsDiscardedAndReported()
    {
        var contract = ValidContract();
        contract.OutfitRules!.Add(
            new OutfitRule { Id = 2, SourceTag = "ghost", RelatedTag = "phantom", Relation = OutfitRelation.Incompatible });
        contract.OutfitRules.Add(
            new OutfitRule { Id = 3, SourceTag = "dress", RelatedTag = "phantom", Relation = OutfitRelation.Requires });

        var result = _service.Validate(contract);

        // Requires survives when one tag exists; Incompatible needs both.
        Assert.Equal([1, 3], result.Contract!.OutfitRules!.Select(r => r.Id));
        var issue = Assert.Single(result.Issues, i => i.Kind == ContractIssueKind.OutfitRuleDiscarded);
        Assert.Equal("ghost, phantom", issue.Subject);
    }

    [Fact]
    public void Validate_InvalidWardrobeAndTaskEntries_AreDiscardedOrClamped()
    {
        var contract = ValidContract();
        contract.Wardrobe!.Add(new ClothingItem { Id = 3, Name = "", Layers = [3] });
        contract.Wardrobe.Add(new ClothingItem { Id = 4, Name = "no layer", Layers = [9] });
        contract.Wardrobe.Add(new ClothingItem { Id = 1, Name = "duplicate id", Layers = [2] });
        contract.Wardrobe.Add(new ClothingItem { Id = 5, Name = "heavy", Layers = [2], Multiplier = 500 });
        contract.Tasks!.Add(new Chore { Id = 2, Name = "  " });
        contract.Tasks.Add(new Chore { Id = 3, Name = "sweeping", FrequencyDays = 0, Value = -4 });

        var result = _service.Validate(contract);

        Assert.Equal(["dress", "stockings", "heavy"], result.Contract!.Wardrobe!.Select(i => i.Name));
        Assert.Equal(100, result.Contract.Wardrobe!.Single(i => i.Name == "heavy").Multiplier);
        Assert.Equal(3, result.Issues.Count(i => i.Kind == ContractIssueKind.WardrobeItemDiscarded));
        Assert.Equal(1, result.Issues.Count(i => i.Kind == ContractIssueKind.TaskDiscarded));
        var sweeping = result.Contract.Tasks!.Single(t => t.Name == "sweeping");
        Assert.Equal(1, sweeping.FrequencyDays);
        Assert.Equal(0m, sweeping.Value);
    }

    [Fact]
    public void Validate_NoUsableWardrobe_IsFatal()
    {
        var contract = ValidContract();
        contract.Wardrobe = [new ClothingItem { Id = 9, Name = "", Layers = [1] }];

        var result = _service.Validate(contract);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Kind == ContractIssueKind.EmptyWardrobe);
    }

    [Fact]
    public void Apply_FailedValidation_Throws_AndChangesNothing()
    {
        var before = _profile.GetGameSettings().IdleWageAmount;
        var result = _service.Validate("""{"schemaVersion":99}""");

        Assert.Throws<InvalidOperationException>(() => _service.Apply(result));

        Assert.Equal(before, _profile.GetGameSettings().IdleWageAmount);
        Assert.Equal(2, _profile.GetWardrobe().Count);
    }

    [Fact]
    public void Apply_ReplacesGameRules_ButKeepsDeviceLevelSettings()
    {
        var current = _profile.GetGameSettings();
        current.PlayerName = "Alyssa";
        current.Language = "it";
        current.SelectedCameraIndex = 2;
        current.AutoSaveIntervalMinutes = 7;
        current.IdleWageAmount = 5;
        _profile.SaveGameSettings(current);

        var contract = ValidContract();
        contract.Settings!.IdleWageAmount = 42;
        contract.Settings.PlayerName = "ignored";
        contract.Wardrobe = [new ClothingItem { Id = 10, Name = "apron", Layers = [4], Tags = ["apron"] }];
        contract.OutfitRules = [];
        contract.Tasks = [];

        _service.Apply(_service.Validate(_service.Serialize(contract)));

        var settings = _profile.GetGameSettings();
        Assert.Equal(42, settings.IdleWageAmount);
        Assert.Equal("Alyssa", settings.PlayerName);
        Assert.Equal("it", settings.Language);
        Assert.Equal(2, settings.SelectedCameraIndex);
        Assert.Equal(7, settings.AutoSaveIntervalMinutes);
        Assert.Equal(["apron"], _profile.GetWardrobe().Select(i => i.Name));
        Assert.Empty(_profile.GetOutfitRules());
        Assert.Empty(_profile.GetChores());
    }
}
