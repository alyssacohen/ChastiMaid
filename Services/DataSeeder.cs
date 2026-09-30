using ChastiMaid.Repositories;
using ChastiMaid.Services.Content;
using ChastiMaid.Services.Contracts;

namespace ChastiMaid.Services;

/// <summary>
///     Seeds an empty profile on first run from the language's seed contract
///     (<c>Content/{language}/default.contract.json</c>), through the same validate-and-apply path as a
///     contract import. Falls back to <see cref="MinimalContentSeeder" /> when that file is missing or invalid.
/// </summary>
public static class DataSeeder
{
    public static void SeedIfEmpty(ProfileRepository profile, string language)
    {
        if (profile.GetWardrobe().Count > 0 || profile.GetOutfitRules().Count > 0 || profile.GetChores().Count > 0)
        {
            return;
        }

        var service = new GameContractService(profile);
        var result = service.Validate(MinimalContentSeeder.Contract);
        try
        {
            var seed = service.Validate(File.ReadAllText(SeedPath(language)));
            if (seed.Contract is not null)
            {
                result = seed;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        service.Apply(result);
    }

    public static string SeedPath(string language) =>
        Path.Combine(AppPaths.Root, "Content", language, "default.contract.json");
}
