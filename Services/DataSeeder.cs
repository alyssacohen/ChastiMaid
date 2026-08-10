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
            var validTags = profile.GetWardrobe().SelectMany(i => i.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var (rules, _) = loader.LoadAndValidateOutfitRules(validTags);
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

        if (profile.GetPermissionTypes().Count == 0)
        {
            foreach (var config in DefaultPermissionTypes())
            {
                profile.SavePermissionType(config);
            }
        }
    }

    private static IEnumerable<PermissionTypeConfig> DefaultPermissionTypes()
    {
        yield return new PermissionTypeConfig
        { Kind = PermissionKind.Pee, BaseGrantPercent = 70, DurationMinutes = 5, CooldownMinutes = 10 };
        yield return new PermissionTypeConfig
        { Kind = PermissionKind.Poop, BaseGrantPercent = 50, DurationMinutes = 8, CooldownMinutes = 10 };
        yield return new PermissionTypeConfig
        { Kind = PermissionKind.SitDown, BaseGrantPercent = 20, DurationMinutes = 3, CooldownMinutes = 20 };
        yield return new PermissionTypeConfig
        { Kind = PermissionKind.Smoke, BaseGrantPercent = 20, DurationMinutes = 5, CooldownMinutes = 30 };
        yield return new PermissionTypeConfig
        { Kind = PermissionKind.TeaCoffee, BaseGrantPercent = 60, DurationMinutes = 10, CooldownMinutes = 30 };
    }
}
