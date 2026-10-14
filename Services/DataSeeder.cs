using ChastiMaid.Localization;
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
    public static void SeedIfEmpty(ProfileRepository profile, string language, ILocalizer l)
    {
        if (profile.GetWardrobe().Count > 0 || profile.GetOutfitRules().Count > 0 || profile.GetChores().Count > 0)
        {
            return;
        }

        Seed(profile, language, l);
    }

    /// <summary>Replaces the profile content with the language's seed contract, whatever is there now.</summary>
    public static void Seed(ProfileRepository profile, string language, ILocalizer l)
    {
        var service = new GameContractService(profile);
        // Language seed first, then English, then the in-code minimal contract.
        foreach (var lang in new[] { language, "en" }.Distinct())
        {
            try
            {
                var seed = service.Validate(File.ReadAllText(SeedPath(lang)));
                if (seed.Contract is not null)
                {
                    service.Apply(seed);
                    return;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        service.Apply(service.Validate(MinimalContentSeeder.Contract(l)));
    }

    public static string SeedPath(string language) =>
        Path.Combine(AppPaths.Root, "Content", language, "default.contract.json");
}
