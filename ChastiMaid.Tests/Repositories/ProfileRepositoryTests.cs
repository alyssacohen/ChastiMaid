using ChastiMaid.Models;
using ChastiMaid.Repositories;
using LiteDB;

namespace ChastiMaid.Tests.Repositories;

public class ProfileRepositoryTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly ProfileRepository _repo;

    public ProfileRepositoryTests() => _repo = new ProfileRepository(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetWardrobe_Initially_ReturnsEmpty()
    {
        Assert.Empty(_repo.GetWardrobe());
    }

    [Fact]
    public void SaveClothingItem_ThenGetWardrobe_ContainsItem()
    {
        _repo.SaveClothingItem(new ClothingItem { Id = 1, Name = "test shirt", Layers = ["outer"], Slots = ["torso"] });

        var wardrobe = _repo.GetWardrobe();
        Assert.Single(wardrobe);
        Assert.Equal("test shirt", wardrobe[0].Name);
    }

    [Fact]
    public void DeleteClothingItem_RemovesItem()
    {
        _repo.SaveClothingItem(new ClothingItem { Id = 1, Name = "to delete", Layers = ["outer"], Slots = ["torso"] });
        _repo.DeleteClothingItem(1);

        Assert.Empty(_repo.GetWardrobe());
    }

    [Fact]
    public void SaveClothingItem_Upsert_UpdatesExisting()
    {
        _repo.SaveClothingItem(new ClothingItem { Id = 1, Name = "original", Layers = ["outer"], Slots = ["torso"] });
        _repo.SaveClothingItem(new ClothingItem { Id = 1, Name = "updated",  Layers = ["outer"], Slots = ["torso"] });

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
        Assert.Equal(0.05, loaded.DailyInterestRate, precision: 5);
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
}
