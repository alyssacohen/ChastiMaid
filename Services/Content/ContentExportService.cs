using ChastiMaid.Repositories;

namespace ChastiMaid.Services.Content;

public class ContentExportService
{
    private readonly WardrobeContentService _wardrobeService = new();
    private readonly OutfitRuleContentService _ruleService = new();
    private readonly TaskContentService _taskService = new();

    public void ExportAll(ProfileRepository repo, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        _wardrobeService.Export(repo.GetWardrobe(), Path.Combine(folderPath, "wardrobe.json"));
        _ruleService.Export(repo.GetOutfitRules(), Path.Combine(folderPath, "outfit-rules.json"));
        _taskService.Export(repo.GetChores(), Path.Combine(folderPath, "tasks.json"));
    }
}
