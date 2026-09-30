using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Services.Content;
using ChastiMaid.Services.Contracts;
using LiteDB;

namespace ChastiMaid.Tests.Services;

public class DataSeederTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public void ShippedSeedContract_IsValid(string language)
    {
        using var db = new LiteDatabase(":memory:");
        var result = new GameContractService(new ProfileRepository(db))
            .Validate(File.ReadAllText(DataSeeder.SeedPath(language)));

        Assert.NotNull(result.Contract);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void SeedIfEmpty_MissingSeedFile_FallsBackToMinimalContract()
    {
        using var db = new LiteDatabase(":memory:");
        var profile = new ProfileRepository(db);

        DataSeeder.SeedIfEmpty(profile, "xx");

        Assert.Equal(MinimalContentSeeder.Contract.Wardrobe!.Count, profile.GetWardrobe().Count);
        Assert.Equal(MinimalContentSeeder.Contract.PermissionTypes!.Count, profile.GetPermissionTypes().Count);
    }

    [Fact]
    public void SeedIfEmpty_ExistingProfile_IsLeftUntouched()
    {
        using var db = new LiteDatabase(":memory:");
        var profile = new ProfileRepository(db);
        DataSeeder.SeedIfEmpty(profile, "xx");
        profile.ReplaceChores([]);
        var wardrobe = profile.GetWardrobe().Count;

        DataSeeder.SeedIfEmpty(profile, "en");

        Assert.Equal(wardrobe, profile.GetWardrobe().Count);
        Assert.Empty(profile.GetChores());
    }
}
