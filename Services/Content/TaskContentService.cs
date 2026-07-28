using System.Text.Json;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

public class TaskContentService
{
    public List<Chore> Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<Chore>>(json, ContentJsonOptions.Options) ?? [];
    }

    public void Export(List<Chore> chores, string filePath)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(chores, ContentJsonOptions.Options));
    }
}
