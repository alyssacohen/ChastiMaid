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
            new() { Id = 1, SourceTag = "girdle", RelatedTag = "stockings", Relation = OutfitRelation.Requires },
        };

        _service.Export(rules, _tempFile);
        var loaded = _service.Load(_tempFile);

        var rule = Assert.Single(loaded);
        Assert.Equal("girdle", rule.SourceTag);
        Assert.Equal("stockings", rule.RelatedTag);
        Assert.Equal(OutfitRelation.Requires, rule.Relation);
    }

    [Fact]
    public void Export_SerializesRelationAsCamelCaseString()
    {
        _service.Export([new OutfitRule { Id = 1, SourceTag = "a", RelatedTag = "b", Relation = OutfitRelation.Incompatible }], _tempFile);

        Assert.Contains("\"incompatible\"", File.ReadAllText(_tempFile));
    }

    [Fact]
    public void Validate_RequiresRule_SurvivesIfEitherTagIsValid()
    {
        var rules = new List<OutfitRule>
        {
            new() { Id = 1, SourceTag = "a", RelatedTag = "missing", Relation = OutfitRelation.Requires },
            new() { Id = 2, SourceTag = "missing", RelatedTag = "a", Relation = OutfitRelation.Requires },
        };

        var (valid, discarded) = _service.Validate(rules, ["a"]);

        Assert.Equal(2, valid.Count);
        Assert.Empty(discarded);
    }

    [Fact]
    public void Validate_RequiresRule_DiscardedIfBothTagsMissing()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, SourceTag = "x", RelatedTag = "y", Relation = OutfitRelation.Requires } };

        var (valid, discarded) = _service.Validate(rules, ["a"]);

        Assert.Empty(valid);
        var d = Assert.Single(discarded);
        Assert.Equal(["x", "y"], d.MissingTags);
    }

    [Fact]
    public void Validate_IncompatibleRule_DiscardedIfEitherTagMissing()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, SourceTag = "a", RelatedTag = "missing", Relation = OutfitRelation.Incompatible } };

        var (valid, discarded) = _service.Validate(rules, ["a"]);

        Assert.Empty(valid);
        var d = Assert.Single(discarded);
        Assert.Equal(["missing"], d.MissingTags);
    }

    [Fact]
    public void Validate_IncompatibleRule_SurvivesIfBothTagsValid()
    {
        var rules = new List<OutfitRule> { new() { Id = 1, SourceTag = "a", RelatedTag = "b", Relation = OutfitRelation.Incompatible } };

        var (valid, discarded) = _service.Validate(rules, ["a", "b"]);

        Assert.Single(valid);
        Assert.Empty(discarded);
    }
}
