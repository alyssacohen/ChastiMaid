using ChastiMaid.Repositories;
using ChastiMaid.Services.Content;
using LiteDB;

namespace ChastiMaid.Tests.Services.Content;

public class ContentLoaderServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "ChastiMaidContentLoaderTests_" + Guid.NewGuid())).FullName;

    private readonly ContentLoaderService _loader = new("en");

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void LoadWardrobe_MissingFile_FallsBackToMinimalSeed()
    {
        var loaded = _loader.LoadWardrobe(Path.Combine(_dir, "missing.json"));

        Assert.Equal(MinimalContentSeeder.Wardrobe.Count, loaded.Count);
    }

    [Fact]
    public void LoadWardrobe_CorruptFile_FallsBackToMinimalSeed()
    {
        var path = Path.Combine(_dir, "wardrobe.json");
        File.WriteAllText(path, "{ not valid json");

        var loaded = _loader.LoadWardrobe(path);

        Assert.Equal(MinimalContentSeeder.Wardrobe.Count, loaded.Count);
    }

    [Fact]
    public void LoadTasks_CorruptFile_DoesNotAffectWardrobeLoading()
    {
        var wardrobePath = Path.Combine(_dir, "wardrobe.json");
        File.WriteAllText(wardrobePath, """[{"id":1,"name":"dress"}]""");
        var tasksPath = Path.Combine(_dir, "tasks.json");
        File.WriteAllText(tasksPath, "not json at all");

        var wardrobe = _loader.LoadWardrobe(wardrobePath);
        var tasks = _loader.LoadTasks(tasksPath);

        Assert.Single(wardrobe);
        Assert.Equal("dress", wardrobe[0].Name);
        Assert.Equal(MinimalContentSeeder.Tasks.Count, tasks.Count);
    }

    [Fact]
    public void ImportAll_ProducesResultWithDiscardedRules()
    {
        File.WriteAllText(Path.Combine(_dir, "wardrobe.json"),
            """[{"id":1,"name":"dress","tags":["dress"]},{"id":2,"name":"shoes","tags":["shoes"]}]""");
        File.WriteAllText(Path.Combine(_dir, "outfit-rules.json"),
            """[{"id":1,"sourceTag":"dress","relatedTag":"shoes","relation":"requires"},{"id":2,"sourceTag":"dress","relatedTag":"missing","relation":"incompatible"}]""");
        File.WriteAllText(Path.Combine(_dir, "tasks.json"), """[{"id":1,"name":"Dusting"}]""");

        using var db = new LiteDatabase(":memory:");
        var repo = new ProfileRepository(db);

        var result = _loader.ImportAll(_dir, repo);

        Assert.Equal(2, result.WardrobeImported);
        Assert.Equal(1, result.RulesImported);
        Assert.Equal(1, result.RulesDiscarded);
        Assert.Equal(["missing"], result.DiscardedRules[0].MissingTags);
        Assert.Equal(1, result.TasksImported);
    }
}
