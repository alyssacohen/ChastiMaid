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
        // Language seed first, then English, then the in-code minimal contract.
        foreach (var lang in new[] { language, "en" }.Distinct())
        {
            try
            {
                var seed = service.Validate(File.ReadAllText(SeedPath(lang)));
                if (seed.Contract is not null)
                {
                    result = seed;
                    break;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        service.Apply(result);
    }

    public static string SeedPath(string language) =>
        Path.Combine(AppPaths.Root, "Content", language, "default.contract.json");
}
