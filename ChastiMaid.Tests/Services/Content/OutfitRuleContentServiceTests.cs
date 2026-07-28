using ChastiMaid.Models;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Services.Content;

public class OutfitRuleContentServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();
    private readonly OutfitRuleContentService _service = new();

    public void Dispose()
    {
        File.Delete(_tempFile);
    }

    [Fact]
    public void ExportThenLoad_RoundTripsEqualRules()
    {
        var rules = new List<OutfitRule>
        {
            new() { Id = 1, ItemId = 3, RelatedItemId = 8, Relation = OutfitRelation.Requires },
        };

        _service.Export(rules, _tempFile);
        var loaded = _service.Load(_tempFile);

        var rule = Assert.Single(loaded);
        Assert.Equal(3, rule.ItemId);
        Assert.Equal(8, rule.RelatedItemId);
        Assert.Equal(OutfitRelation.Requires, rule.Relation);
    }

    [Fact]
    public void Export_SerializesRelationAsCamelCaseString()
    {
        _service.Export([new OutfitRule { Id = 1, ItemId = 1, RelatedItemId = 2, Relation = OutfitRelation.Incompatible }], _tempFile);

        Assert.Contains("\"incompatible\"", File.ReadAllText(_tempFile));
    }

    [Fact]
    public void Validate_RequiresRule_SurvivesIfEitherIdIsValid()
    {
        var rules = new List<OutfitRule>
        {
            new() { Id = 1, ItemId = 1, RelatedItemId = 999, Relation = OutfitRelation.Requires },
            new() { Id = 2, ItemId = 999, RelatedItemId = 1, Relation = OutfitRelation.Requires },
        };

        var (valid, discarded) = _service.Validate(rules, [1]);

        Assert.Equal(2, valid.Count);
        Assert.Empty(discarded);
    }

    [Fact]
    public void Validate_RequiresRule_DiscardedIfBothIdsMissing()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, ItemId = 998, RelatedItemId = 999, Relation = OutfitRelation.Requires } };

        var (valid, discarded) = _service.Validate(rules, [1]);

        Assert.Empty(valid);
        var d = Assert.Single(discarded);
        Assert.Equal([998, 999], d.MissingItemIds);
    }

    [Fact]
    public void Validate_IncompatibleRule_DiscardedIfEitherIdMissing()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, ItemId = 1, RelatedItemId = 999, Relation = OutfitRelation.Incompatible } };

        var (valid, discarded) = _service.Validate(rules, [1]);

        Assert.Empty(valid);
        var d = Assert.Single(discarded);
        Assert.Equal([999], d.MissingItemIds);
    }

    [Fact]
    public void Validate_IncompatibleRule_SurvivesIfBothIdsValid()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, ItemId = 1, RelatedItemId = 2, Relation = OutfitRelation.Incompatible } };

        var (valid, discarded) = _service.Validate(rules, [1, 2]);

        Assert.Single(valid);
        Assert.Empty(discarded);
    }
}
