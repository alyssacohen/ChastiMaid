using ChastiMaid.Models;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Services.Content;

public class OutfitRuleContentServiceTests
{
    private readonly OutfitRuleContentService _service = new();

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
