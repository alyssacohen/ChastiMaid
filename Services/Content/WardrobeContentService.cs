using System.Text.Json;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

public class WardrobeContentService
{
    public List<ClothingItem> Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<ClothingItem>>(json, ContentJsonOptions.Options) ?? [];
    }

    public void Export(List<ClothingItem> items, string filePath)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(items, ContentJsonOptions.Options));
    }
}
