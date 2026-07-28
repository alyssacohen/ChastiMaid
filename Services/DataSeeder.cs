using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Services;

/// <summary>
///     Seeds the profile database with default content on first run. Wardrobe, outfit rules, and
///     tasks are seeded independently, since <see cref="ContentLoaderService" /> loads each one
///     independently and they may fall out of sync (e.g. a later partial content import).
/// </summary>
public static class DataSeeder
{
    public static void SeedIfEmpty(ProfileRepository profile, ContentLoaderService loader)
    {
        var isFirstRun = profile.GetWardrobe().Count == 0 && profile.GetOutfitRules().Count == 0
            && profile.GetChores().Count == 0;

        if (profile.GetWardrobe().Count == 0)
        {
            foreach (var item in loader.LoadWardrobe())
            {
                profile.SaveClothingItem(item);
            }
        }

        if (profile.GetOutfitRules().Count == 0)
        {
            var (rules, _) = loader.LoadAndValidateOutfitRules(profile.GetWardrobe().Select(i => i.Id).ToList());
            foreach (var rule in rules)
            {
                profile.SaveOutfitRule(rule);
            }
        }

        if (profile.GetChores().Count == 0)
        {
            foreach (var chore in loader.LoadTasks())
            {
                profile.SaveChore(chore);
            }
        }

        if (isFirstRun)
        {
            profile.SaveGameSettings(new GameSettings());
        }
    }
}
