using ChastiMaid.Models;
using ChastiMaid.Repositories;
using LiteDB;

namespace ChastiMaid.Tests.Repositories;

public class ProfileRepositoryTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly ProfileRepository _repo;

    public ProfileRepositoryTests()
    {
        _repo = new ProfileRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void GetWardrobe_Initially_ReturnsEmpty()
    {
        Assert.Empty(_repo.GetWardrobe());
    }

    [Fact]
    public void SaveClothingItem_ThenGetWardrobe_ContainsItem()
    {
        _repo.SaveClothingItem(new ClothingItem
            { Id = 1, Name = "test shirt", Layers = [3], Slots = ["torso"] });

        var wardrobe = _repo.GetWardrobe();
        Assert.Single(wardrobe);
        Assert.Equal("test shirt", wardrobe[0].Name);
    }

    [Fact]
    public void DeleteClothingItem_RemovesItem()
    {
        _repo.SaveClothingItem(new ClothingItem
            { Id = 1, Name = "to delete", Layers = [3], Slots = ["torso"] });
        _repo.DeleteClothingItem(1);

        Assert.Empty(_repo.GetWardrobe());
    }

    [Fact]
    public void SaveClothingItem_Upsert_UpdatesExisting()
    {
        _repo.SaveClothingItem(
            new ClothingItem { Id = 1, Name = "original", Layers = [3], Slots = ["torso"] });
        _repo.SaveClothingItem(new ClothingItem
            { Id = 1, Name = "updated", Layers = [3], Slots = ["torso"] });

        var wardrobe = _repo.GetWardrobe();
        Assert.Single(wardrobe);
        Assert.Equal("updated", wardrobe[0].Name);
    }

    [Fact]
    public void GetGameSettings_WhenEmpty_ReturnsDefault()
    {
        var settings = _repo.GetGameSettings();

        Assert.NotNull(settings);
    }

    [Fact]
    public void SaveGameSettings_ThenGet_ReturnsSavedValues()
    {
        var saved = new GameSettings { Id = 1, DailyInterestRate = 0.05 };
        _repo.SaveGameSettings(saved);

        var loaded = _repo.GetGameSettings();
        Assert.Equal(0.05, loaded.DailyInterestRate, 5);
    }

    [Fact]
    public void GetChores_Initially_ReturnsEmpty()
    {
        Assert.Empty(_repo.GetChores());
    }

    [Fact]
    public void SaveChore_ThenDelete_RemovesChore()
    {
        _repo.SaveChore(new Chore { Id = 1, Name = "dusting", Value = 10 });
        _repo.DeleteChore(1);

        Assert.Empty(_repo.GetChores());
    }

    [Fact]
    public void GetOutfitRules_Initially_ReturnsEmpty()
    {
        Assert.Empty(_repo.GetOutfitRules());
    }

    [Fact]
    public void SaveAndGetStats_RoundTrips()
    {
        var stats = new PlayerStats { Id = 1, TotalSessions = 7 };
        _repo.SaveStats(stats);

        Assert.Equal(7, _repo.GetStats().TotalSessions);
    }

    [Fact]
    public void ReplaceWardrobe_RemovesItemsNotInNewSet()
    {
        _repo.SaveClothingItem(new ClothingItem { Id = 1, Name = "old", Layers = [3], Slots = ["torso"] });

        _repo.ReplaceWardrobe([new ClothingItem { Id = 2, Name = "new", Layers = [3], Slots = ["torso"] }]);

        var wardrobe = _repo.GetWardrobe();
        Assert.Single(wardrobe);
        Assert.Equal("new", wardrobe[0].Name);
    }

    [Fact]
    public void ReplaceOutfitRules_RemovesRulesNotInNewSet()
    {
        _repo.SaveOutfitRule(new OutfitRule { Id = 1, SourceTag = "a", RelatedTag = "b", Relation = OutfitRelation.Requires });

        _repo.ReplaceOutfitRules([new OutfitRule { Id = 2, SourceTag = "c", RelatedTag = "d", Relation = OutfitRelation.Incompatible }]);

        var rules = _repo.GetOutfitRules();
        Assert.Single(rules);
        Assert.Equal(2, rules[0].Id);
    }

    [Fact]
    public void ReplaceChores_RemovesChoresNotInNewSet()
    {
        _repo.SaveChore(new Chore { Id = 1, Name = "old", Value = 10 });

        _repo.ReplaceChores([new Chore { Id = 2, Name = "new", Value = 20 }]);

        var chores = _repo.GetChores();
        Assert.Single(chores);
        Assert.Equal("new", chores[0].Name);
    }
}
