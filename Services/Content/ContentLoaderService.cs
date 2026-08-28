using System.Globalization;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

/// <summary>
///     Loads per-language wardrobe/outfit-rules/tasks JSON content, falling back independently per
///     file to <see cref="MinimalContentSeeder" /> when a file is missing or fails to parse.
/// </summary>
public class ContentLoaderService(string language)
{
    private static readonly string[] SupportedLanguages = ["en", "it"];

    private readonly WardrobeContentService _wardrobeService = new();
    private readonly OutfitRuleContentService _ruleService = new();
    private readonly TaskContentService _taskService = new();

    /// <summary>
    ///     Resolves "system"/"en"/"it" to a concrete supported language code, matching
    ///     <c>App.ApplyLanguage</c>'s resolution logic.
    /// </summary>
    public static string ResolveLanguage(string language)
    {
        if (language != "system")
        {
            return SupportedLanguages.Contains(language) ? language : "en";
        }

        return SupportedLanguages.FirstOrDefault(
            l => CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == l) ?? "en";
    }

    private string ContentDir => Path.Combine(AppPaths.Root, "Content", ResolveLanguage(language));

    public List<ClothingItem> LoadWardrobe()
    {
        return LoadWardrobe(Path.Combine(ContentDir, "wardrobe.json"));
    }

    public List<ClothingItem> LoadWardrobe(string filePath)
    {
        try
        {
            return _wardrobeService.Load(filePath);
        }
        catch (Exception)
        {
            return MinimalContentSeeder.Wardrobe;
        }
    }

    public (List<OutfitRule> Rules, List<DiscardedOutfitRuleInfo> Discarded) LoadAndValidateOutfitRules(
        IReadOnlyCollection<string> validTags)
    {
        return LoadAndValidateOutfitRules(Path.Combine(ContentDir, "outfit-rules.json"), validTags);
    }

    public (List<OutfitRule> Rules, List<DiscardedOutfitRuleInfo> Discarded) LoadAndValidateOutfitRules(
        string filePath, IReadOnlyCollection<string> validTags)
    {
        List<OutfitRule> rules;
        try
        {
            rules = _ruleService.Load(filePath);
        }
        catch (Exception)
        {
            rules = MinimalContentSeeder.OutfitRules;
        }

        return _ruleService.Validate(rules, validTags);
    }

    public List<Chore> LoadTasks()
    {
        return LoadTasks(Path.Combine(ContentDir, "tasks.json"));
    }

    public List<Chore> LoadTasks(string filePath)
    {
        try
        {
            return _taskService.Load(filePath);
        }
        catch (Exception)
        {
            return MinimalContentSeeder.Tasks;
        }
    }

    /// <summary>
    ///     Imports wardrobe unconditionally, then validates outfit rules against the just-imported
    ///     wardrobe ids (discarding any referencing missing items), then imports tasks independently.
    /// </summary>
    public ImportResult ImportAll(string folderPath, Repositories.ProfileRepository profile)
    {
        var wardrobe = _wardrobeService.Load(Path.Combine(folderPath, "wardrobe.json"));
        profile.ReplaceWardrobe(wardrobe);

        var rawRules = _ruleService.Load(Path.Combine(folderPath, "outfit-rules.json"));
        var validTags = wardrobe.SelectMany(i => i.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (validRules, discarded) = _ruleService.Validate(rawRules, validTags);
        profile.ReplaceOutfitRules(validRules);

        var tasks = _taskService.Load(Path.Combine(folderPath, "tasks.json"));
        profile.ReplaceChores(tasks);

        return new ImportResult(wardrobe.Count, validRules.Count, discarded.Count, discarded, tasks.Count);
    }
}
